namespace SellBotLk.Api.Models.DTOs;

public class WhatsAppIncomingDto
{
    public string Object { get; set; } = null!;
    public List<WhatsAppEntryDto> Entry { get; set; } = new();
}

public class WhatsAppEntryDto
{
    public string Id { get; set; } = null!;
    public List<WhatsAppChangeDto> Changes { get; set; } = new();
}

public class WhatsAppChangeDto
{
    public WhatsAppValueDto Value { get; set; } = null!;
    public string Field { get; set; } = null!;
}

public class WhatsAppValueDto
{
    public string MessagingProduct { get; set; } = null!;
    public WhatsAppMetadataDto Metadata { get; set; } = null!;
    public List<WhatsAppContactDto>? Contacts { get; set; }
    public List<WhatsAppMessageItemDto>? Messages { get; set; }
}

public class WhatsAppMetadataDto
{
    public string DisplayPhoneNumber { get; set; } = null!;
    public string PhoneNumberId { get; set; } = null!;
}

public class WhatsAppContactDto
{
    public WhatsAppProfileDto Profile { get; set; } = null!;
    public string WaId { get; set; } = null!;
}

public class WhatsAppProfileDto
{
    public string Name { get; set; } = null!;
}

public class WhatsAppMessageItemDto
{
    public string From { get; set; } = null!;
    public string Id { get; set; } = null!;
    public string Timestamp { get; set; } = null!;
    public string Type { get; set; } = null!;
    public WhatsAppTextDto? Text { get; set; }
    public WhatsAppMediaDto? Image { get; set; }
    public WhatsAppMediaDto? Audio { get; set; }
    public WhatsAppMediaDto? Document { get; set; }
}

public class WhatsAppTextDto
{
    public string Body { get; set; } = null!;
}

public class WhatsAppMediaDto
{
    public string Id { get; set; } = null!;
    public string? MimeType { get; set; }
    public string? Sha256 { get; set; }
    public string? Filename { get; set; }
}