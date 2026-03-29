using Microsoft.EntityFrameworkCore;
using SellBotLk.Api.Data;
using SellBotLk.Api.Integrations.Gemini;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Models.Entities;

namespace SellBotLk.Api.Services;

public class DeliveryService
{
    private readonly AppDbContext _db;
    private readonly GeminiService _geminiService;
    private readonly WhatsAppSendService _whatsAppSendService;
    private readonly ILogger<DeliveryService> _logger;

    public DeliveryService(
        AppDbContext db,
        GeminiService geminiService,
        WhatsAppSendService whatsAppSendService,
        ILogger<DeliveryService> logger)
    {
        _db = db;
        _geminiService = geminiService;
        _whatsAppSendService = whatsAppSendService;
        _logger = logger;
    }

    /// <summary>
    /// Checks if a delivery area is serviceable and returns fee + ETA.
    /// Uses Gemini to fuzzy-match the customer's typed area to a zone name.
    /// Example: "kolombo 3" → "Colombo", "kandy city" → "Kandy"
    /// </summary>
    public async Task<DeliveryCheckResponseDto> CheckZoneAsync(
        string customerArea, decimal orderTotal)
    {
        var zones = await _db.DeliveryZones
            .Where(z => z.IsActive)
            .AsNoTracking()
            .ToListAsync();

        // Try exact match first (case-insensitive)
        var zone = zones.FirstOrDefault(z =>
            z.ZoneName.Equals(customerArea.Trim(),
                StringComparison.OrdinalIgnoreCase));

        // If no exact match, use Gemini to fuzzy match
        if (zone == null)
        {
            var zoneNames = string.Join(", ", zones.Select(z => z.ZoneName));
            var prompt =
                $"A customer typed this delivery area: \"{customerArea}\"\n\n" +
                $"Available delivery zones: {zoneNames}\n\n" +
                $"Return ONLY the single best matching zone name from the list above, " +
                $"exactly as written. If nothing matches, return: NONE\n" +
                $"No explanation — just the zone name or NONE.";

            var matched = await _geminiService.GenerateReplyAsync(prompt, "en");
            matched = matched.Trim().Trim('"');

            if (matched != "NONE")
                zone = zones.FirstOrDefault(z =>
                    z.ZoneName.Equals(matched,
                        StringComparison.OrdinalIgnoreCase));
        }

        if (zone == null)
        {
            return new DeliveryCheckResponseDto
            {
                IsServiceable = false,
                ZoneName = customerArea,
                DeliveryFee = 0,
                EstimatedDays = 0,
                IsFreeDelivery = false,
                Message = $"Sorry, we don't currently deliver to " +
                         $"\"{customerArea}\". Please contact us for more info."
            };
        }

        // Check free delivery threshold
        var isFreeDelivery = zone.FreeDeliveryThreshold.HasValue &&
                             orderTotal >= zone.FreeDeliveryThreshold.Value;

        var actualFee = isFreeDelivery ? 0 : zone.DeliveryFee;

        var message = isFreeDelivery
            ? $"✅ Free delivery to {zone.ZoneName}! " +
              $"Estimated {zone.EstimatedDays} day(s). 🎉"
            : $"Delivery to {zone.ZoneName}: LKR {zone.DeliveryFee:N0}. " +
              $"Estimated {zone.EstimatedDays} day(s).";

        if (!isFreeDelivery && zone.FreeDeliveryThreshold.HasValue)
        {
            var remaining = zone.FreeDeliveryThreshold.Value - orderTotal;
            message += $"\n💡 Add LKR {remaining:N0} more to get free delivery!";
        }

        return new DeliveryCheckResponseDto
        {
            IsServiceable = true,
            ZoneName = zone.ZoneName,
            DeliveryFee = actualFee,
            EstimatedDays = zone.EstimatedDays,
            IsFreeDelivery = isFreeDelivery,
            Message = message
        };
    }

    /// <summary>
    /// Updates the delivery status of an order and notifies the customer.
    /// Valid transitions:
    /// Confirmed → Processing → Dispatched → OutForDelivery → Delivered
    /// </summary>
    public async Task<bool> UpdateDeliveryStatusAsync(
        int orderId, string newStatus, string? driverNote = null)
    {
        var order = await _db.Orders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found for status update",
                orderId);
            return false;
        }

        if (!Enum.TryParse<OrderStatus>(newStatus, out var status))
        {
            _logger.LogWarning("Invalid status: {Status}", newStatus);
            return false;
        }

        // Validate transition
        if (!IsValidTransition(order.Status, status))
        {
            _logger.LogWarning(
                "Invalid status transition: {From} → {To} for order {OrderId}",
                order.Status, status, orderId);
            return false;
        }

        var previousStatus = order.Status;
        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(driverNote))
            order.DriverNote = driverNote;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Order {OrderNumber} status updated: {From} → {To}",
            order.OrderNumber, previousStatus, status);

        // Notify customer
        await SendStatusNotificationAsync(order);

        return true;
    }

    /// <summary>
    /// Returns all active delivery zones.
    /// </summary>
    public async Task<List<DeliveryZoneResponseDto>> GetAllZonesAsync()
    {
        var zones = await _db.DeliveryZones
            .Where(z => z.IsActive)
            .AsNoTracking()
            .OrderBy(z => z.ZoneName)
            .ToListAsync();

        return zones.Select(z => new DeliveryZoneResponseDto
        {
            Id = z.Id,
            ZoneName = z.ZoneName,
            DeliveryFee = z.DeliveryFee,
            EstimatedDays = z.EstimatedDays,
            FreeDeliveryThreshold = z.FreeDeliveryThreshold,
            IsActive = z.IsActive
        }).ToList();
    }

    private async Task SendStatusNotificationAsync(Order order)
    {
        if (order.Customer == null) return;

        var message = order.Status switch
        {
            OrderStatus.Processing =>
                $"🔧 Your order {order.OrderNumber} is being processed!\n" +
                $"We're preparing your items for dispatch.",

            OrderStatus.Dispatched =>
                $"🚚 Your order {order.OrderNumber} has been dispatched!\n" +
                $"Delivery area: {order.DeliveryArea ?? "N/A"}\n" +
                (string.IsNullOrEmpty(order.DriverNote)
                    ? ""
                    : $"Note: {order.DriverNote}\n") +
                $"We'll notify you when it's out for delivery.",

            OrderStatus.Delivered =>
                $"✅ Your order {order.OrderNumber} has been delivered!\n\n" +
                $"Thank you for shopping with us! 🙏\n" +
                $"We hope you love your new furniture. " +
                $"Please don't hesitate to contact us if you need anything.",

            _ => null
        };

        if (message == null) return;

        await _whatsAppSendService.SendTextMessageAsync(
            order.Customer.PhoneNumber, message);

        _logger.LogInformation(
            "Status notification sent to customer for order {OrderNumber}",
            order.OrderNumber);
    }

    private static bool IsValidTransition(
        OrderStatus current, OrderStatus next)
    {
        return (current, next) switch
        {
            (OrderStatus.Confirmed, OrderStatus.Processing) => true,
            (OrderStatus.Processing, OrderStatus.Dispatched) => true,
            (OrderStatus.Dispatched, OrderStatus.Delivered) => true,
            (OrderStatus.Pending, OrderStatus.Confirmed) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            (OrderStatus.Confirmed, OrderStatus.Cancelled) => true,
            (OrderStatus.Processing, OrderStatus.Cancelled) => true,
            (OrderStatus.FraudPending, OrderStatus.Confirmed) => true,
            (OrderStatus.FraudPending, OrderStatus.Cancelled) => true,
            _ => false
        };
    }
}