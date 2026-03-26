using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SellBotLk.Api.Models.Entities;

/// <summary>
/// Represents a WhatsApp customer who interacts with the business.
/// Phone number is the primary identifier — sourced directly from WhatsApp.
/// </summary>
[Table("Customers")]
public class Customer
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// WhatsApp number in E.164 format (e.g. +94771234567).
    /// Enforced unique at DB level. Never store without country code.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = null!;

    /// <summary>
    /// Customer name extracted from WhatsApp profile or conversation.
    /// Nullable — not all customers share their name.
    /// </summary>
    [MaxLength(100)]
    public string? Name { get; set; }

    /// <summary>
    /// Detected language code: "en" (English), "si" (Sinhala), "ta" (Tamil).
    /// Auto-detected by Gemini on first message. Persisted here to maintain
    /// language consistency across all future conversations.
    /// </summary>
    [MaxLength(10)]
    public string PreferredLanguage { get; set; } = "en";

    /// <summary>
    /// Running count of all orders placed by this customer.
    /// Incremented by OrderService on every confirmed order.
    /// Used to determine customer tier: New (&lt;3), Regular (3–10), VIP (&gt;10).
    /// </summary>
    public int TotalOrders { get; set; } = 0;

    /// <summary>
    /// Lifetime spend in LKR. Updated atomically when each order is confirmed.
    /// Drives VIP status detection and personalised discount eligibility.
    /// </summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal TotalSpend { get; set; } = 0.00m;

    /// <summary>
    /// Timestamp of the customer's most recent confirmed order.
    /// Used to personalise greetings: "Welcome back! It's been 3 weeks since your last order."
    /// </summary>
    public DateTime? LastOrderDate { get; set; }

    /// <summary>
    /// When true, this customer is blocked from placing new orders.
    /// Set by FraudDetectionService or manually by admin after review.
    /// Blocked customers receive a polite "contact us" message instead of order processing.
    /// </summary>
    public bool IsBlocked { get; set; } = false;

    /// <summary>
    /// AI-generated profile notes. Gemini summarises the customer's
    /// preferences, common products, and interaction patterns here.
    /// Injected into prompts to enable personalised responses.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Timestamp of record creation. Set once on INSERT, never updated.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ──────────────────────────────────────────────

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}