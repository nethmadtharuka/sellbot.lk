using SellBotLk.Api.Data.Repositories;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Models.Entities;

namespace SellBotLk.Api.Services;

public class OrderService
{
    private readonly OrderRepository _orderRepository;
    private readonly ProductRepository _productRepository;
    private readonly OrderNumberGenerator _orderNumberGenerator;
    private readonly IConfiguration _config;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        OrderRepository orderRepository,
        ProductRepository productRepository,
        OrderNumberGenerator orderNumberGenerator,
        IConfiguration config,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _orderNumberGenerator = orderNumberGenerator;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new order, deducts stock atomically,
    /// and returns the saved order with all details.
    /// </summary>
    public async Task<OrderResponseDto> CreateOrderAsync(CreateOrderDto dto)
    {
        // 1 — Validate all products exist and have enough stock
        var orderItems = new List<(Product Product, int Quantity, decimal? NegotiatedPrice)>();

        foreach (var item in dto.Items)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId);

            if (product == null)
                throw new Exception($"Product {item.ProductId} not found.");

            if (!product.IsActive)
                throw new Exception($"Product '{product.Name}' is no longer available.");

            if (product.StockQty < item.Quantity)
                throw new Exception(
                    $"Insufficient stock for '{product.Name}'. " +
                    $"Available: {product.StockQty}, Requested: {item.Quantity}");

            orderItems.Add((product, item.Quantity, item.NegotiatedPrice));
        }

        // 2 — Generate order number
        var orderNumber = await _orderNumberGenerator.GenerateAsync();

        // 3 — Calculate totals
        decimal totalAmount = 0;
        decimal discountAmount = 0;

        foreach (var (product, quantity, negotiatedPrice) in orderItems)
        {
            var effectivePrice = negotiatedPrice ?? product.Price;

            // Apply bulk discount if applicable and no negotiation
            if (negotiatedPrice == null &&
                quantity >= product.BulkMinQty &&
                product.BulkDiscountPercent > 0)
            {
                var discount = effectivePrice * (product.BulkDiscountPercent / 100m);
                discountAmount += discount * quantity;
                effectivePrice -= discount;
            }

            totalAmount += effectivePrice * quantity;
        }

        // 4 — Create order entity
        var order = new Order
        {
            OrderNumber = orderNumber,
            CustomerId = dto.CustomerId,
            Status = OrderStatus.Confirmed,
            TotalAmount = totalAmount,
            DiscountAmount = discountAmount,
            PaymentStatus = PaymentStatus.Unpaid,
            DeliveryAddress = dto.DeliveryAddress,
            DeliveryArea = dto.DeliveryArea,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = orderItems.Select(x => new OrderItem
            {
                ProductId = x.Product.Id,
                Quantity = x.Quantity,
                UnitPrice = x.Product.Price,
                NegotiatedPrice = x.NegotiatedPrice
            }).ToList()
        };

        // 5 — Save order
        var saved = await _orderRepository.CreateAsync(order);

        // 6 — Deduct stock for each item
        foreach (var (product, quantity, _) in orderItems)
        {
            await _productRepository.DeductStockAsync(product.Id, quantity);

            _logger.LogInformation(
                "Stock deducted — Product: {Name}, Qty: {Qty}, Remaining: {Left}",
                product.Name, quantity, product.StockQty - quantity);
        }

        _logger.LogInformation(
            "Order created — {OrderNumber}, Total: LKR {Total}",
            orderNumber, totalAmount);

        return MapToDto(saved);
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

        var updated = await _orderRepository.UpdateStatusAsync(id, statusEnum);
        return updated == null ? null : MapToDto(updated);
    }

    public async Task<OrderResponseDto?> CancelOrderAsync(int id)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null) return null;

        if (order.Status == OrderStatus.Delivered)
            throw new Exception("Cannot cancel a delivered order.");

        if (order.Status == OrderStatus.Cancelled)
            throw new Exception("Order is already cancelled.");

        // Restore stock for each item
        foreach (var item in order.Items)
        {
            await _productRepository.RestockAsync(item.ProductId, item.Quantity);

            _logger.LogInformation(
                "Stock restored — Product: {Id}, Qty: {Qty}",
                item.ProductId, item.Quantity);
        }

        var updated = await _orderRepository.UpdateStatusAsync(id, OrderStatus.Cancelled);

        _logger.LogInformation("Order cancelled — {OrderNumber}", order.OrderNumber);

        return updated == null ? null : MapToDto(updated);
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

        return string.Join("\n", lines);
    }

    private static OrderResponseDto MapToDto(Order o) => new()
    {
        Id = o.Id,
        OrderNumber = o.OrderNumber,
        CustomerName = o.Customer?.Name ?? "",
        CustomerPhone = o.Customer?.PhoneNumber ?? "",
        Status = o.Status.ToString(),
        PaymentStatus = o.PaymentStatus.ToString(),
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