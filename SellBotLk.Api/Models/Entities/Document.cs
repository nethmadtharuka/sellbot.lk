using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SellBotLk.Api.Models.Entities;

/// <summary>
/// Type of document submitted by a customer or the business owner.
/// Determines which DocumentService handler and Gemini extraction prompt is used.
/// </summary>
public enum DocumentType
{
    /// <summary>
    /// Invoice issued to a customer. May trigger payment tracking.
    /// </summary>
    CustomerBill = 0,

    /// <summary>
    /// Invoice from a supplier/vendor. Triggers automatic inventory restock
    /// via InventoryRestockService after Gemini extracts line items.
    /// </summary>
    SupplierInvoice = 1,

    /// <summary>
    /// Bank transfer screenshot or receipt sent by a customer as proof of payment.
    /// PaymentVerificationService extracts amount + reference and matches to an order.
    /// </summary>
    PaymentSlip = 2,

    /// <summary>
    /// Photo of damaged goods received by a customer.
    /// Creates a complaint record and triggers owner notification.
    /// </summary>
    DamageReport = 3
}

/// <summary>
/// Represents any document (PDF, image) submitted to the system.
/// Gemini Vision/PDF extraction is used to parse all document types.
/// Extracted structured data is stored as JSON in ExtractedData.
///
/// This single table handles all document types to keep queries simple.
/// DocumentType discriminates between them for routing and business logic.
/// </summary>
[Table("Documents")]
public class Document
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Document category. Determines processing pipeline and extraction prompt.
    /// </summary>
    [Required]
    public DocumentType Type { get; set; }

    /// <summary>
    /// Customer who sent this document (nullable — owner-uploaded invoices
    /// have no associated customer).
    /// </summary>
    public int? CustomerId { get; set; }

    /// <summary>
    /// URL of the original uploaded image or PDF.
    /// Stored in file system or object storage (e.g. local path or cloud URL).
    /// Payment slip images are stored encrypted.
    /// </summary>
    [MaxLength(500)]
    public string? RawImageUrl { get; set; }

    /// <summary>
    /// JSON blob of structured data extracted by Gemini.
    ///
    /// Schema varies by DocumentType:
    ///
    /// SupplierInvoice:
    ///   { "vendor": "ABC Furniture", "date": "2026-03-18",
    ///     "items": [{ "name": "Office Chair", "qty": 10, "unitPrice": 12000 }],
    ///     "total": 120000 }
    ///
    /// PaymentSlip:
    ///   { "bank": "Commercial Bank", "amount": 45000, "reference": "TXN-20260318-001",
    ///     "date": "2026-03-18" }
    ///
    /// DamageReport:
    ///   { "description": "Leg broken on delivery", "productHint": "dining chair",
    ///     "severity": "moderate" }
    ///
    /// Stored as JSON column for flexibility — schema evolves per document type
    /// without requiring migrations for every new field Gemini starts extracting.
    /// </summary>
    [Required]
    [Column(TypeName = "json")]
    public string ExtractedData { get; set; } = "{}";

    /// <summary>
    /// Vendor/supplier name extracted from invoice.
    /// Stored as a top-level column (not just in JSON) so it can be indexed
    /// and used in dashboard filters without JSON parsing.
    /// </summary>
    [MaxLength(200)]
    public string? VendorName { get; set; }

    /// <summary>
    /// Total monetary amount extracted from the document (LKR).
    /// For invoices: total due. For payment slips: amount transferred.
    /// </summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal? TotalAmount { get; set; }

    /// <summary>
    /// Date printed on the document (not the upload date).
    /// </summary>
    public DateOnly? DocumentDate { get; set; }

    /// <summary>
    /// False = document extracted but inventory/payment not yet actioned.
    /// True = downstream effects applied (stock updated or payment confirmed).
    /// Guards against double-processing if a document is resubmitted.
    /// </summary>
    public bool IsProcessed { get; set; } = false;

    /// <summary>
    /// AI notes on document quality, extraction confidence, or issues encountered.
    /// Example: "Low image contrast — amount may be approximate."
    ///          "Product 'Rattan Sofa' not found in catalogue — owner alerted."
    /// Shown to admin in the document viewer.
    /// </summary>
    public string? ProcessingNotes { get; set; }

    /// <summary>
    /// Timestamp of record creation (upload time).
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ──────────────────────────────────────────────

    [ForeignKey(nameof(CustomerId))]
    public Customer? Customer { get; set; }
}