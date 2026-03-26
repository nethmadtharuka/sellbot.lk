using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SellBotLk.Api.Models.Entities;

/// <summary>
/// Represents a single line item within an order.
/// Each OrderItem links one Product to one Order with quantity
/// and the price captured at the time the order was placed.
///
/// WHY we capture price here:
/// Product prices can change. By storing UnitPrice (and NegotiatedPrice)
/// at order time, historical order values remain accurate regardless of
/// future price updates.
/// </summary>
[Table("OrderItems")]
public class OrderItem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the parent order.
    /// </summary>
    [Required]
    public int OrderId { get; set; }

    /// <summary>
    /// Foreign key to the product ordered.
    /// </summary>
    [Required]
    public int ProductId { get; set; }

    /// <summary>
    /// Number of units ordered. Validated to be >= 1.
    /// If quantity >= Product.BulkMinQty, BulkDiscountPercent is applied.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; }

    /// <summary>
    /// Product's standard selling price at the time this order was placed.
    /// Sourced from Product.Price at order creation — never modified afterwards.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Final agreed price per unit after AI negotiation, if any occurred.
    /// Null = no negotiation happened; the standard UnitPrice applies.
    /// NegotiationService sets this only when a counteroffer was accepted.
    /// Must always be >= Product.MinPrice (enforced by NegotiationService).
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? NegotiatedPrice { get; set; }

    /// <summary>
    /// Convenience property: effective price per unit used for totals.
    /// Returns NegotiatedPrice if set, otherwise UnitPrice.
    /// Not mapped to DB column — calculated on read.
    /// </summary>
    [NotMapped]
    public decimal EffectiveUnitPrice => NegotiatedPrice ?? UnitPrice;

    /// <summary>
    /// Line total: EffectiveUnitPrice × Quantity.
    /// Not mapped to DB — always calculated to avoid stale data.
    /// </summary>
    [NotMapped]
    public decimal LineTotal => EffectiveUnitPrice * Quantity;

    // ── Navigation properties ──────────────────────────────────────────────

    [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; } = null!;

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;
}