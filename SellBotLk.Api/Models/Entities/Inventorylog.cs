using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SellBotLk.Api.Models.Entities;

/// <summary>
/// Reason for an inventory stock change.
/// Every change to Product.StockQty must be recorded with one of these types.
/// </summary>
public enum InventoryChangeType
{
    /// <summary>
    /// Stock reduced because a customer order was confirmed.
    /// ReferenceId = OrderNumber (e.g. "ORD-2026-042").
    /// </summary>
    OrderDeduction = 0,

    /// <summary>
    /// Stock increased from a supplier invoice processed via DocumentService.
    /// ReferenceId = Document.Id as string.
    /// </summary>
    SupplierRestock = 1,

    /// <summary>
    /// Manual stock correction entered from the admin dashboard.
    /// ReferenceId = admin username who made the change.
    /// </summary>
    ManualAdjust = 2,

    /// <summary>
    /// Stock restored because an order was cancelled.
    /// ReferenceId = OrderNumber of the cancelled order.
    /// </summary>
    Return = 3
}

/// <summary>
/// Immutable audit trail of every stock quantity change for every product.
///
/// KEY DESIGN DECISIONS:
/// - Records are NEVER updated or deleted — insert only.
/// - QuantityBefore and QuantityAfter are stored explicitly (not just delta)
///   so the log is self-describing even without querying Product.
/// - ForecastingService reads this table to calculate sales velocity
///   (average daily OrderDeduction entries per product over last 90 days).
/// - InventoryAlertJob reads this table to detect when StockQty is approaching
///   LowStockThreshold.
/// </summary>
[Table("InventoryLogs")]
public class InventoryLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Product whose stock level changed.
    /// </summary>
    [Required]
    public int ProductId { get; set; }

    /// <summary>
    /// Nature of the inventory change. Determines which service created this log.
    /// </summary>
    [Required]
    public InventoryChangeType ChangeType { get; set; }

    /// <summary>
    /// Stock quantity BEFORE this change was applied.
    /// Combined with QuantityAfter, this gives the delta: After - Before.
    /// </summary>
    [Required]
    public int QuantityBefore { get; set; }

    /// <summary>
    /// Stock quantity AFTER this change was applied.
    /// This should match Product.StockQty at time of recording.
    /// </summary>
    [Required]
    public int QuantityAfter { get; set; }

    /// <summary>
    /// Optional reference linking this log to its source event.
    /// OrderDeduction → OrderNumber (e.g. "ORD-2026-042")
    /// SupplierRestock → Document ID as string (e.g. "DOC-88")
    /// ManualAdjust   → Admin username
    /// Return         → OrderNumber of cancelled order
    /// </summary>
    [MaxLength(50)]
    public string? ReferenceId { get; set; }

    /// <summary>
    /// Timestamp when this log entry was created.
    /// Set to UTC now on insert. Never modified.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ──────────────────────────────────────────────

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;
}