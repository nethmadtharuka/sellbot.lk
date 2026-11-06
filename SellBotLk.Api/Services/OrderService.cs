using Microsoft.EntityFrameworkCore;
using SellBotLk.Api.Data;
using SellBotLk.Api.Data.Repositories;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Models.Entities;

namespace SellBotLk.Api.Services;

public class OrderService
{
    private const int DefaultUnpaidExpiryHours = 24;

    private readonly AppDbContext _db;
    private readonly OrderRepository _orderRepository;
    private readonly ProductRepository _productRepository;
    private readonly WhatsAppSendService _whatsAppSendService;
    private readonly IConfiguration _config;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        AppDbContext db,
        OrderRepository orderRepository,
        ProductRepository productRepository,
        WhatsAppSendService whatsAppSendService,
        IConfiguration config,
        ILogger<OrderService> logger)
    {
        _db = db;
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _whatsAppSendService = whatsAppSendService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Creates an order and takes its stock in one transaction, so either both
    /// happen or neither does. Every price and quantity rule lives here because
    /// every order, from WhatsApp or the admin API, passes through this method.
    /// </summary>
    public async Task<OrderResponseDto> CreateOrderAsync(CreateOrderDto dto)
    {
        // 1 — Validate quantities, products and prices
        if (dto.Items == null || dto.Items.Count == 0)
            throw new Exception("An order must contain at least one item.");

        var orderItems = new List<(Product Product, int Quantity, decimal? NegotiatedPrice)>();

        foreach (var item in dto.Items)
        {
            if (item.Quantity < 1)
                throw new Exception(
                    $"Quantity must be at least 1 for product {item.ProductId}.");

            var product = await _productRepository.GetByIdAsync(item.ProductId);

            if (product == null)
                throw new Exception($"Product {item.ProductId} not found.");

            if (!product.IsActive)
                throw new Exception($"Product '{product.Name}' is no longer available.");

            if (item.NegotiatedPrice is decimal price && !IsAllowedPrice(price, product))
                throw new Exception(
                    $"The price requested for '{product.Name}' is not allowed.");

            orderItems.Add((product, item.Quantity, item.NegotiatedPrice));
        }

        // 2 — Calculate totals
        decimal totalAmount = 0;
        decimal discountAmount = 0;

        foreach (var (product, quantity, negotiatedPrice) in orderItems)
        {
            var effectivePrice = negotiatedPrice ?? product.Price;

            // Apply bulk discount if applicable and no negotiation.
            // A discount may never take the price below the product's floor.
            if (negotiatedPrice == null &&
                quantity >= product.BulkMinQty &&
                product.BulkDiscountPercent > 0)
            {
                var discounted = Math.Max(
                    product.Price * (1 - product.BulkDiscountPercent / 100m),
                    product.MinPrice);

                if (discounted < product.Price)
                {
                    discountAmount += (product.Price - discounted) * quantity;
                    effectivePrice = discounted;
                }
            }

            totalAmount += effectivePrice * quantity;
        }

        // 3 — Build the order. The real number comes from its database ID, which
        // can't collide; the placeholder only satisfies the unique index until then.
        var now = DateTime.UtcNow;
        var order = new Order
        {
            OrderNumber = $"TMP-{Guid.NewGuid():N}"[..20],
            CustomerId = dto.CustomerId,
            Status = OrderStatus.Confirmed,
            TotalAmount = totalAmount,
            DiscountAmount = discountAmount,
            PaymentStatus = PaymentStatus.Unpaid,
            DeliveryAddress = dto.DeliveryAddress,
            DeliveryArea = dto.DeliveryArea,
            Notes = dto.Notes,
            CreatedAt = now,
            UpdatedAt = now,
            Items = orderItems.Select(x => new OrderItem
            {
                ProductId = x.Product.Id,
                Quantity = x.Quantity,
                UnitPrice = x.Product.Price,
                NegotiatedPrice = x.NegotiatedPrice
            }).ToList()
        };

        // Combine repeated lines, and lock products in a fixed order so two
        // multi-item orders can't deadlock each other.
        var stockNeeded = orderItems
            .GroupBy(x => x.Product.Id)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(x => x.Quantity), g.First().Product))
            .OrderBy(x => x.ProductId)
            .ToList();

        // 4 — Take the stock, then save the order, atomically. Stock goes first:
        // inserting order items takes shared locks on their products, and two orders
        // holding those would deadlock when each tried to upgrade to an update lock.
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var deductions = new List<(int ProductId, int Quantity, int Remaining)>();

            foreach (var (productId, quantity, product) in stockNeeded)
            {
                var remaining = await _productRepository.TryDeductStockAsync(productId, quantity);

                if (remaining == null)
                    throw new Exception(
                        $"Insufficient stock for '{product.Name}'. " +
                        $"Available: {product.StockQty}, Requested: {quantity}");

                deductions.Add((productId, quantity, remaining.Value));

                _logger.LogInformation(
                    "Stock deducted — Product: {Name}, Qty: {Qty}, Remaining: {Left}",
                    product.Name, quantity, remaining);
            }

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            order.OrderNumber = $"ORD-{now.Year}-{order.Id:D3}";

            foreach (var (productId, quantity, remaining) in deductions)
                _productRepository.LogStockChange(
                    productId, InventoryChangeType.OrderDeduction,
                    remaining + quantity, remaining, order.OrderNumber);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();

            // The rollback leaves these tracked; a later save must not re-insert them.
            _productRepository.DiscardPendingLogs();
            foreach (var item in order.Items)
                _db.Entry(item).State = EntityState.Detached;
            _db.Entry(order).State = EntityState.Detached;

            throw;
        }

        _logger.LogInformation(
            "Order created — {OrderNumber}, Total: LKR {Total}",
            order.OrderNumber, totalAmount);

        // Reload so the response has product and customer names.
        return MapToDto((await _orderRepository.GetByIdAsync(order.Id))!);
    }

    public async Task<OrderResponseDto?> GetByIdAsync(int id)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        return order == null ? null : MapToDto(order);
    }

    public async Task<OrderResponseDto?> GetByOrderNumberAsync(string orderNumber)
    {
        var order = await _orderRepository.GetByOrderNumberAsync(orderNumber);
        return order == null ? null : MapToDto(order);
    }

    public async Task<List<OrderResponseDto>> GetAllAsync(
        string? status = null, int? customerId = null)
    {
        OrderStatus? statusEnum = null;
        if (status != null && Enum.TryParse<OrderStatus>(status, true, out var parsed))
            statusEnum = parsed;

        var orders = await _orderRepository.GetAllAsync(statusEnum, customerId);
        return orders.Select(MapToDto).ToList();
    }

    public async Task<OrderResponseDto?> UpdateStatusAsync(int id, string newStatus)
    {
        if (!Enum.TryParse<OrderStatus>(newStatus, true, out var statusEnum))
            throw new Exception($"Invalid status: {newStatus}");

        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null) return null;

        // Validate state transition
        if (order.Status == OrderStatus.Delivered)
            throw new Exception("Cannot update a delivered order.");

        if (order.Status == OrderStatus.Cancelled)
            throw new Exception("Cannot update a cancelled order.");

        if (statusEnum == OrderStatus.Cancelled)
            return await CancelOrderAsync(id);

        if (!await _orderRepository.TryUpdateStatusAsync(id, order.Status, statusEnum))
            throw new Exception("The order changed while updating it. Please refresh and try again.");

        await _db.Entry(order).ReloadAsync();
        return MapToDto(order);
    }

    /// <summary>
    /// The only way an order becomes Cancelled. Every path goes through here so
    /// stock is restored exactly once: the conditional update lets a single caller
    /// win, even if several try at the same moment.
    /// With onlyIfUnpaid, returns null instead of cancelling an order that has
    /// since received a payment slip.
    /// </summary>
    public async Task<OrderResponseDto?> CancelOrderAsync(int id, bool onlyIfUnpaid = false)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null) return null;

        if (order.Status == OrderStatus.Delivered)
            throw new Exception("Cannot cancel a delivered order.");

        if (order.Status == OrderStatus.Cancelled)
            throw new Exception("Order is already cancelled.");

        var now = DateTime.UtcNow;

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var rows = await _db.Orders
                .Where(o => o.Id == id &&
                            o.Status != OrderStatus.Delivered &&
                            o.Status != OrderStatus.Cancelled &&
                            (!onlyIfUnpaid || o.PaymentStatus == PaymentStatus.Unpaid))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OrderStatus.Cancelled)
                    .SetProperty(o => o.UpdatedAt, now));

            if (rows == 0)
            {
                if (onlyIfUnpaid) return null;
                throw new Exception("Order is already cancelled or delivered.");
            }

            // Restore stock for each product, in the same lock order as order creation
            foreach (var item in order.Items
                         .GroupBy(i => i.ProductId)
                         .Select(g => (ProductId: g.Key, Quantity: g.Sum(i => i.Quantity)))
                         .OrderBy(x => x.ProductId))
            {
                await _productRepository.RestockAsync(
                    item.ProductId, item.Quantity,
                    InventoryChangeType.Return, order.OrderNumber);

                _logger.LogInformation(
                    "Stock restored — Product: {Id}, Qty: {Qty}",
                    item.ProductId, item.Quantity);
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            _productRepository.DiscardPendingLogs();
            throw;
        }

        await _db.Entry(order).ReloadAsync();

        _logger.LogInformation("Order cancelled — {OrderNumber}", order.OrderNumber);

        return MapToDto(order);
    }

    /// <summary>
    /// Cancels orders that never received a slip within the configured window, which
    /// releases their stock. Orders already in review, paid, or being processed by an
    /// admin are left alone. Returns the number cancelled.
    /// </summary>
    public async Task<int> ExpireUnpaidOrdersAsync()
    {
        var hours = UnpaidExpiryHours;
        if (hours <= 0) return 0;

        // UpdatedAt, not CreatedAt: a rejected slip restarts the customer's window.
        var cutoff = DateTime.UtcNow.AddHours(-hours);
        var ids = await _db.Orders
            .AsNoTracking()
            .Where(o => o.PaymentStatus == PaymentStatus.Unpaid &&
                        (o.Status == OrderStatus.Pending || o.Status == OrderStatus.Confirmed) &&
                        o.UpdatedAt < cutoff)
            .Select(o => o.Id)
            .ToListAsync();

        var expired = 0;
        foreach (var id in ids)
        {
            try
            {
                var order = await CancelOrderAsync(id, onlyIfUnpaid: true);
                if (order == null) continue;

                expired++;
                _logger.LogInformation("Unpaid order expired — {OrderNumber}", order.OrderNumber);

                var language = await _db.Orders
                    .Where(o => o.Id == id)
                    .Select(o => o.Customer.PreferredLanguage)
                    .FirstAsync();

                await _whatsAppSendService.SendTextMessageAsync(
                    order.CustomerPhone, FormatExpiryMessage(order.OrderNumber, language));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to expire unpaid order {Id}", id);
            }
        }

        return expired;
    }

    /// <summary>
    /// Formats a confirmed order as a WhatsApp confirmation message
    /// including bank transfer instructions so the customer knows how to pay.
    /// </summary>
    public string FormatOrderConfirmationMessage(OrderResponseDto order)
    {
        var lines = new List<string>
        {
            $"✅ *Order Confirmed!*",
            $"📋 Order Number: *{order.OrderNumber}*\n"
        };

        foreach (var item in order.Items)
        {
            lines.Add($"• {item.ProductName} x{item.Quantity} " +
                     $"— LKR {item.LineTotal:N0}");
        }

        if (order.DiscountAmount > 0)
            lines.Add($"\n🏷️ Discount: -LKR {order.DiscountAmount:N0}");

        lines.Add($"\n💰 *Total: LKR {order.TotalAmount:N0}*");

        if (!string.IsNullOrEmpty(order.DeliveryArea))
            lines.Add($"🚚 Delivery to: {order.DeliveryArea}");

        var bankName = _config["Payment:BankName"] ?? "Bank of Ceylon";
        var accountNumber = _config["Payment:AccountNumber"] ?? "0000000000";
        var accountHolder = _config["Payment:AccountHolder"] ?? "SellBot.lk";

        lines.Add($"\n💳 *To complete your order:*");
        lines.Add($"1. Transfer LKR {order.TotalAmount:N0} to:");
        lines.Add($"   Bank: *{bankName}*");
        lines.Add($"   Account: *{accountNumber}*");
        lines.Add($"   Name: *{accountHolder}*");
        lines.Add($"2. Send a screenshot of your payment slip here");
        lines.Add($"\nYour order will be confirmed once we verify the payment. 🙏");

        if (UnpaidExpiryHours > 0)
            lines.Add($"⏰ We hold your items for {UnpaidExpiryHours} hours while waiting for your slip.");

        return string.Join("\n", lines);
    }

    private int UnpaidExpiryHours =>
        _config.GetValue("Orders:UnpaidExpiryHours", DefaultUnpaidExpiryHours);

    /// <summary>
    /// A negotiated price must be positive, at or above the floor, and never more
    /// than the list price. The database product is the only source of these limits.
    /// </summary>
    private static bool IsAllowedPrice(decimal price, Product product) =>
        price > 0 && price >= product.MinPrice && price <= product.Price;

    private static string FormatExpiryMessage(string orderNumber, string language) => language switch
    {
        "si" => $"⏰ ඇණවුම {orderNumber} සඳහා නියමිත කාලය තුළ ගෙවීමක් නොලැබුණු බැවින් එය අවලංගු කරන ලදී. " +
                "අවශ්‍ය නම් නැවත ඇණවුම් කළ හැකිය.",
        "ta" => $"⏰ ஆர்டர் {orderNumber} க்கு குறிப்பிட்ட நேரத்தில் கட்டணம் கிடைக்காததால் அது ரத்து செய்யப்பட்டது. " +
                "விரும்பினால் மீண்டும் ஆர்டர் செய்யலாம்.",
        _ => $"⏰ Order {orderNumber} was cancelled because we didn't receive payment in time. " +
             "You're welcome to order again."
    };

    private static OrderResponseDto MapToDto(Order o) => new()
    {
        Id = o.Id,
        OrderNumber = o.OrderNumber,
        CustomerName = o.Customer?.Name ?? "",
        CustomerPhone = o.Customer?.PhoneNumber ?? "",
        Status = o.Status.ToString(),
        PaymentStatus = o.PaymentStatus.ToString(),
        PaymentReference = o.PaymentReference,
        TotalAmount = o.TotalAmount,
        DiscountAmount = o.DiscountAmount,
        DeliveryAddress = o.DeliveryAddress,
        DeliveryArea = o.DeliveryArea,
        Notes = o.Notes,
        IsFraudFlagged = o.IsFraudFlagged,
        FraudReason = o.FraudReason,
        CreatedAt = o.CreatedAt,
        UpdatedAt = o.UpdatedAt,
        Items = o.Items.Select(i => new OrderItemResponseDto
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductName = i.Product?.Name ?? "",
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            NegotiatedPrice = i.NegotiatedPrice,
            EffectiveUnitPrice = i.EffectiveUnitPrice,
            LineTotal = i.LineTotal
        }).ToList()
    };
}
