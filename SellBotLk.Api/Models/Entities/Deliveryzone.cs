using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SellBotLk.Api.Models.Entities;

/// <summary>
/// Represents a delivery coverage area with its associated fee and ETA.
///
/// Used by DeliveryService to:
/// 1. Check if a customer's area is serviceable.
/// 2. Calculate the delivery fee to add to an order.
/// 3. Estimate delivery days to include in order confirmations.
/// 4. Apply free delivery when an order exceeds FreeDeliveryThreshold.
///
/// Seeded in the initial migration with Sri Lankan coverage areas.
/// Business owner can add/edit zones from the admin dashboard.
/// </summary>
[Table("DeliveryZones")]
public class DeliveryZone
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Human-readable zone name matching how customers refer to their area.
    /// Examples: "Colombo", "Kandy", "Galle", "Jaffna", "Negombo".
    ///
    /// DeliveryService uses fuzzy matching (via Gemini) to map a customer's
    /// typed address to the nearest ZoneName — customers may type "kolombo"
    /// or "col 3" and the AI resolves it to "Colombo".
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ZoneName { get; set; } = null!;

    /// <summary>
    /// Fixed delivery charge in LKR for this zone.
    /// Added to Order.TotalAmount after the customer confirms their address.
    /// Set to 0.00 for zones with always-free delivery.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(8,2)")]
    public decimal DeliveryFee { get; set; }

    /// <summary>
    /// Typical number of business days for delivery to this zone.
    /// Shown to customers in order confirmations: "Estimated delivery: 2 days".
    /// </summary>
    [Required]
    [Range(1, 30, ErrorMessage = "Estimated delivery days must be between 1 and 30.")]
    public int EstimatedDays { get; set; }

    /// <summary>
    /// Minimum order total in LKR that qualifies for free delivery to this zone.
    /// Null = no free delivery threshold (delivery fee always applies).
    ///
    /// Example: FreeDeliveryThreshold = 50000 means orders above LKR 50,000
    /// to this zone get free delivery. DeliveryService applies this check
    /// before adding the delivery fee to the order.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? FreeDeliveryThreshold { get; set; }

    /// <summary>
    /// When false, this zone is no longer served.
    /// DeliveryService returns "we don't deliver to this area" for inactive zones.
    /// Use IsActive = false instead of deleting — preserves historical order data.
    /// </summary>
    public bool IsActive { get; set; } = true;
}