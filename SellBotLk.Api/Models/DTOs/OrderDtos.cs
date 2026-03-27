namespace SellBotLk.Api.Models.DTOs;

public class CreateOrderDto
{
    public int CustomerId { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
    public string? DeliveryAddress { get; set; }
    public string? DeliveryArea { get; set; }
    public string? Notes { get; set; }
}

public class OrderItemDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal? NegotiatedPrice { get; set; }
}

public class OrderResponseDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public string Status { get; set; } = "";
    public string PaymentStatus { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? DeliveryArea { get; set; }
    public string? Notes { get; set; }
    public bool IsFraudFlagged { get; set; }
    public string? FraudReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<OrderItemResponseDto> Items { get; set; } = new();
}

public class OrderItemResponseDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? NegotiatedPrice { get; set; }
    public decimal EffectiveUnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class UpdateOrderStatusDto
{
    public string Status { get; set; } = "";
}