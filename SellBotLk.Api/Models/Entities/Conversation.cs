using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SellBotLk.Api.Models.Entities;

/// <summary>
/// State of a WhatsApp conversation with a customer.
/// Used by MessageProcessingService to route incoming messages
/// to the correct handler based on where in the flow the customer is.
///
/// State machine:
///   Greeting → Browsing → Ordering → Negotiating → Completed
///                       ↘ Support ↗
/// Any state can return to Greeting after inactivity or explicit reset.
/// </summary>
public enum ConversationState
{
    /// <summary>Customer just said hi or started fresh.</summary>
    Greeting = 0,

    /// <summary>Customer is browsing / asking about products.</summary>
    Browsing = 1,

    /// <summary>Customer has confirmed items and is in the order flow.</summary>
    Ordering = 2,

    /// <summary>Customer is negotiating a price for one or more items.</summary>
    Negotiating = 3,

    /// <summary>Customer is asking about an existing order or reporting an issue.</summary>
    Support = 4,

    /// <summary>
    /// Order confirmed and conversation concluded.
    /// Next message starts a fresh Greeting state.
    /// </summary>
    Completed = 5
}

/// <summary>
/// Tracks the live state of a customer's WhatsApp conversation.
/// One active conversation per customer at any time.
/// Context JSON carries ephemeral mid-flow data (cart items, last shown products, etc.)
/// to maintain continuity across multiple messages.
///
/// Conversations are created on first customer contact and updated on every message.
/// They are NOT a message log — use Serilog for message history.
/// </summary>
[Table("Conversations")]
public class Conversation
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Customer this conversation belongs to.
    /// </summary>
    [Required]
    public int CustomerId { get; set; }

    /// <summary>
    /// Current state in the conversation flow.
    /// Updated by MessageProcessingService after each processed message.
    /// </summary>
    [Required]
    public ConversationState State { get; set; } = ConversationState.Greeting;

    /// <summary>
    /// JSON blob storing ephemeral conversation context.
    /// Content depends on the current State.
    ///
    /// Browsing context example:
    ///   { "lastShownProducts": [12, 45, 67], "lastSearchQuery": "red sofa" }
    ///
    /// Ordering context example:
    ///   { "pendingItems": [{ "productId": 12, "qty": 2 }],
    ///     "deliveryArea": "Kandy" }
    ///
    /// Negotiating context example:
    ///   { "productId": 12, "originalPrice": 18500, "customerOffer": 15000,
    ///     "counterOffer": 17000, "roundsRemaining": 2 }
    ///
    /// Completed / Greeting: null or {}
    ///
    /// This is cleared (set to null) when State transitions back to Greeting.
    /// </summary>
    [Column(TypeName = "json")]
    public string? Context { get; set; }

    /// <summary>
    /// Timestamp of the last message in this conversation.
    /// Updated on every incoming message.
    /// Used to detect stale conversations (>2 hours without activity = reset to Greeting).
    /// </summary>
    [Required]
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ──────────────────────────────────────────────

    [ForeignKey(nameof(CustomerId))]
    public Customer Customer { get; set; } = null!;
}