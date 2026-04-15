using System.Text.Json.Serialization;

namespace SellBotLk.Api.Models.DTOs;

public class WhatsAppIncomingDto
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = null!;

    [JsonPropertyName("entry")]
    public List<WhatsAppEntryDto> Entry { get; set; } = new();
}

public class WhatsAppEntryDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("changes")]
    public List<WhatsAppChangeDto> Changes { get; set; } = new();
}

public class WhatsAppChangeDto
{
    [JsonPropertyName("value")]
    public WhatsAppValueDto Value { get; set; } = null!;

    [JsonPropertyName("field")]
    public string Field { get; set; } = null!;
}

public class WhatsAppValueDto
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; set; } = null!;

    [JsonPropertyName("metadata")]
    public WhatsAppMetadataDto Metadata { get; set; } = null!;

    [JsonPropertyName("contacts")]
    public List<WhatsAppContactDto>? Contacts { get; set; }

    [JsonPropertyName("messages")]
    public List<WhatsAppMessageItemDto>? Messages { get; set; }

    // Status callbacks (e.g., sent/delivered/read) come through here, often without `messages`.
    [JsonPropertyName("statuses")]
    public List<WhatsAppStatusDto>? Statuses { get; set; }
}

public class WhatsAppMetadataDto
{
    [JsonPropertyName("display_phone_number")]
    public string DisplayPhoneNumber { get; set; } = null!;

    [JsonPropertyName("phone_number_id")]
    public string PhoneNumberId { get; set; } = null!;
}

public class WhatsAppContactDto
{
    [JsonPropertyName("profile")]
    public WhatsAppProfileDto? Profile { get; set; }

    [JsonPropertyName("wa_id")]
    public string WaId { get; set; } = null!;

    // Some payloads include this instead of profile details.
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }
}

public class WhatsAppProfileDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class WhatsAppStatusDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("recipient_id")]
    public string? RecipientId { get; set; }

    [JsonPropertyName("recipient_user_id")]
    public string? RecipientUserId { get; set; }
}

public class WhatsAppMessageItemDto
{
    [JsonPropertyName("from")]
    public string From { get; set; } = null!;

    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = null!;

    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;

    [JsonPropertyName("text")]
    public WhatsAppTextDto? Text { get; set; }

    [JsonPropertyName("image")]
    public WhatsAppMediaDto? Image { get; set; }

    [JsonPropertyName("audio")]
    public WhatsAppMediaDto? Audio { get; set; }

    [JsonPropertyName("document")]
    public WhatsAppMediaDto? Document { get; set; }
}

public class WhatsAppTextDto
{
    [JsonPropertyName("body")]
    public string Body { get; set; } = null!;
}

public class WhatsAppMediaDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }
}