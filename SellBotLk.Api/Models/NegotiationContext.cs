namespace SellBotLk.Api.Models;

/// <summary>
/// Stored as JSON in Conversation.Context during price negotiation.
/// Tracks the full negotiation state across multiple WhatsApp messages.
/// </summary>
public class NegotiationContext
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal MinPrice { get; set; }
    public decimal? CustomerLastOffer { get; set; }
    public decimal? CounterOffer { get; set; }
    public int RoundsRemaining { get; set; } = 3;
    public bool IsResolved { get; set; } = false;
}