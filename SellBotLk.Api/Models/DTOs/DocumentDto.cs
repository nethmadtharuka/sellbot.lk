using System.Text.Json.Serialization;
using SellBotLk.Api.Models.Entities;

namespace SellBotLk.Api.Models.DTOs;

public class DocumentResponseDto
{
    public int Id { get; set; }
    public string Type { get; set; } = null!;
    public int? CustomerId { get; set; }
    public string? VendorName { get; set; }
    public decimal? TotalAmount { get; set; }
    public DateOnly? DocumentDate { get; set; }
    public bool IsProcessed { get; set; }
    public string? ProcessingNotes { get; set; }
    public string ExtractedData { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

// What Gemini extracts from a supplier invoice
public class ExtractedInvoiceData
{
    [JsonPropertyName("vendor")]
    public string? Vendor { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("invoiceNumber")]
    public string? InvoiceNumber { get; set; }

    [JsonPropertyName("items")]
    public List<ExtractedInvoiceItem> Items { get; set; } = new();

    [JsonPropertyName("subtotal")]
    public decimal? Subtotal { get; set; }

    [JsonPropertyName("tax")]
    public decimal? Tax { get; set; }

    [JsonPropertyName("total")]
    public decimal? Total { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public class ExtractedInvoiceItem
{
    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = null!;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("lineTotal")]
    public decimal LineTotal { get; set; }
}

// What Gemini extracts from a payment slip
public class ExtractedPaymentData
{
    [JsonPropertyName("bank")]
    public string? Bank { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("senderName")]
    public string? SenderName { get; set; }

    [JsonPropertyName("receiverName")]
    public string? ReceiverName { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 1.0;
}

// What Gemini extracts from a damage report photo
public class ExtractedDamageData
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("productHint")]
    public string? ProductHint { get; set; }

    [JsonPropertyName("severity")]
    public string? Severity { get; set; }

    [JsonPropertyName("recommendation")]
    public string? Recommendation { get; set; }
}

public class ProcessDocumentRequest
{
    public IFormFile File { get; set; } = null!;
    public string DocumentType { get; set; } = null!;
    public int? CustomerId { get; set; }
}