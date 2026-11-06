using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SellBotLk.Api.Models.Entities;

/// <summary>
/// The slip image behind an order's payment, kept so an admin can review it.
/// The unique hash stops the same file being claimed against a second order.
/// Rejecting a slip deletes this row, which lets the customer resubmit it.
/// </summary>
[Table("PaymentSlips")]
public class PaymentSlip
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int OrderId { get; set; }

    /// <summary>Hex SHA-256 of ImageBytes.</summary>
    [Required]
    [MaxLength(64)]
    public string ImageSha256 { get; set; } = null!;

    [Required]
    [Column(TypeName = "longblob")]
    public byte[] ImageBytes { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string MimeType { get; set; } = null!;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; } = null!;
}
