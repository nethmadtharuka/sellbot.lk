namespace SellBotLk.Api.Models.DTOs;

public class ParsedMessageIntent
{
    public string Intent { get; set; } = "Other";
    public string Language { get; set; } = "en";
    public string? CustomerName { get; set; }
    public List<OrderItemRequest>? OrderItems { get; set; }
    public string? ProductSearchQuery { get; set; }
    public string? OrderNumber { get; set; }
    public decimal? OfferedPrice { get; set; }
    public int? ProductId { get; set; }
    public string ReplyMessage { get; set; } = null!;
    public double Confidence { get; set; } = 1.0;
}

public class OrderItemRequest
{
    public string ProductName { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal? OfferedPrice { get; set; }
}