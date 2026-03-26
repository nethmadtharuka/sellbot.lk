using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SellBotLk.Api.Models.Entities;

/// <summary>
/// Represents a product in the business catalogue.
/// Supports text-based smart search AND visual search via Gemini Vision
/// using the Color, Material, and Style attributes.
/// </summary>
[Table("Products")]
public class Product
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Full product name, e.g. "Executive High-Back Leather Chair".
    /// Used in WhatsApp responses and smart search matching.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Product category, e.g. "Chair", "Table", "Sofa", "Wardrobe".
    /// Indexed in DB for fast category filtering.
    /// Gemini uses this during visual search to narrow candidates.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = null!;

    /// <summary>
    /// Full product description sent to customers on request.
    /// Also passed to Gemini during smart text search for semantic matching.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Regular selling price in LKR. Never expose MinPrice to customers.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }

    /// <summary>
    /// Minimum price floor for AI-driven negotiations.
    /// NegotiationService will never accept an offer below this value.
    /// NEVER included in any customer-facing message or API response.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal MinPrice { get; set; }

    /// <summary>
    /// Discount percentage applied automatically for bulk orders.
    /// Example: 10.00 means 10% off when BulkMinQty is reached.
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal BulkDiscountPercent { get; set; } = 0;

    /// <summary>
    /// Minimum quantity required to trigger the bulk discount.
    /// Default 10 — orders of 10+ units automatically get BulkDiscountPercent off.
    /// </summary>
    public int BulkMinQty { get; set; } = 10;

    /// <summary>
    /// Current quantity in stock. Deducted atomically when an order is confirmed.
    /// Orders that would reduce this below zero are rejected with InsufficientStockException.
    /// </summary>
    [Required]
    public int StockQty { get; set; } = 0;

    /// <summary>
    /// When StockQty falls to or below this value, InventoryAlertJob
    /// sends a WhatsApp alert to the business owner.
    /// </summary>
    public int LowStockThreshold { get; set; } = 5;

    /// <summary>
    /// Primary colour used for Gemini Vision visual search matching.
    /// Example: "Brown", "Black", "White", "Natural Wood".
    /// </summary>
    [MaxLength(50)]
    public string? Color { get; set; }

    /// <summary>
    /// Primary material used in visual search attribute matching.
    /// Example: "Leather", "Fabric", "Solid Wood", "Metal Frame".
    /// </summary>
    [MaxLength(100)]
    public string? Material { get; set; }

    /// <summary>
    /// Style tag for visual similarity scoring.
    /// Example: "Modern", "Traditional", "Scandinavian", "Industrial".
    /// </summary>
    [MaxLength(100)]
    public string? Style { get; set; }

    /// <summary>
    /// URL of the product image hosted externally (e.g. cloud storage).
    /// Sent to customers via WhatsApp when showing product matches.
    /// Null = no image available, system will skip image in response.
    /// </summary>
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Soft delete flag. Set to false instead of hard-deleting rows.
    /// Preserves historical order data integrity.
    /// All product queries must filter WHERE IsActive = true.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp of record creation.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ──────────────────────────────────────────────

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<InventoryLog> InventoryLogs { get; set; } = new List<InventoryLog>();
    public ICollection<DailyReport> DailyReports { get; set; } = new List<DailyReport>();
}