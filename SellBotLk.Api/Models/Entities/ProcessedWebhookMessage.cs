using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SellBotLk.Api.Models.Entities;

/// <summary>
/// One row per WhatsApp message ID (wamid) already claimed for processing.
/// Meta can deliver a message more than once; the primary key makes it run once.
/// </summary>
[Table("ProcessedWebhookMessages")]
public class ProcessedWebhookMessage
{
    [Key]
    [MaxLength(191)]
    public string MessageId { get; set; } = null!;

    [Required]
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
