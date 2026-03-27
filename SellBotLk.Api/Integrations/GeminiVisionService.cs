using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SellBotLk.Api.Models.DTOs;

namespace SellBotLk.Api.Integrations.Gemini;

public class GeminiVisionService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<GeminiVisionService> _logger;

    private const string BaseUrl =
        "https://generativelanguage.googleapis.com/v1beta/models";

    public GeminiVisionService(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<GeminiVisionService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Extracts structured data from a supplier invoice image or PDF page.
    /// Returns ExtractedInvoiceData JSON string.
    /// </summary>
    public async Task<string> ExtractInvoiceDataAsync(
        byte[] imageBytes, string mimeType)
    {
        var prompt = """
            You are a document extraction AI for a Sri Lankan furniture business.
            
            Analyze this supplier invoice/bill image and extract ALL data.
            
            Return ONLY a valid JSON object — no markdown, no backticks:
            {
              "vendor": "supplier company name",
              "date": "YYYY-MM-DD format or null",
              "invoiceNumber": "invoice/bill number or null",
              "items": [
                {
                  "productName": "exact product name from invoice",
                  "quantity": 10,
                  "unitPrice": 12000.00,
                  "lineTotal": 120000.00
                }
              ],
              "subtotal": 120000.00,
              "tax": 0.00,
              "total": 120000.00,
              "notes": "any special notes or null"
            }
            
            Rules:
            - Extract ALL line items — do not skip any
            - If amounts are in LKR, keep as numbers without currency symbol
            - If a field is not visible or unclear, use null
            - For dates, convert to YYYY-MM-DD format
            - Be precise with quantities and prices — this affects real inventory
            """;

        return await CallGeminiVisionAsync(imageBytes, mimeType, prompt);
    }

    /// <summary>
    /// Extracts payment details from a bank transfer screenshot.
    /// Returns ExtractedPaymentData JSON string.
    /// </summary>
    public async Task<string> ExtractPaymentDataAsync(
        byte[] imageBytes, string mimeType)
    {
        var prompt = """
            You are a payment verification AI for a Sri Lankan furniture business.
            
            Analyze this bank transfer screenshot or payment slip.
            
            Return ONLY a valid JSON object — no markdown, no backticks:
            {
              "bank": "bank name (e.g. Commercial Bank, BOC, Sampath)",
              "amount": 45000.00,
              "reference": "transaction reference number",
              "date": "YYYY-MM-DD format or null",
              "senderName": "sender account name or null",
              "receiverName": "receiver account name or null",
              "confidence": 0.95
            }
            
            Rules:
            - Amount must be a number without currency symbols
            - Reference number is critical — extract it exactly as shown
            - Confidence: 1.0 = all fields clearly visible, 
              0.5 = some fields unclear, 0.0 = cannot read
            - If this is NOT a payment slip, return confidence: 0.0
            """;

        return await CallGeminiVisionAsync(imageBytes, mimeType, prompt);
    }

    /// <summary>
    /// Extracts damage description from a damage report photo.
    /// </summary>
    public async Task<string> ExtractDamageDataAsync(
        byte[] imageBytes, string mimeType)
    {
        var prompt = """
            You are a damage assessment AI for a Sri Lankan furniture business.
            
            Analyze this photo of damaged furniture/product.
            
            Return ONLY a valid JSON object — no markdown, no backticks:
            {
              "description": "detailed description of the damage",
              "productHint": "what type of product appears to be damaged",
              "severity": "minor/moderate/severe",
              "recommendation": "repair/replace/inspect"
            }
            """;

        return await CallGeminiVisionAsync(imageBytes, mimeType, prompt);
    }

    private async Task<string> CallGeminiVisionAsync(
        byte[] imageBytes, string mimeType, string prompt, int retryCount = 0)
    {
        var apiKey = _config["Gemini:ApiKey"];
        var model = _config["Gemini:Model"] ?? "gemini-1.5-flash";
        var url = $"{BaseUrl}/{model}:generateContent?key={apiKey}";

        var base64Image = Convert.ToBase64String(imageBytes);

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mimeType,
                                data = base64Image
                            }
                        },
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                maxOutputTokens = 2000
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var start = DateTime.UtcNow;
            var response = await _httpClient.PostAsync(url, content);
            var latency = (DateTime.UtcNow - start).TotalMilliseconds;
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode == 429 && retryCount < 2)
                {
                    _logger.LogWarning("Gemini Vision rate limit — retrying");
                    await Task.Delay(TimeSpan.FromSeconds(60));
                    return await CallGeminiVisionAsync(
                        imageBytes, mimeType, prompt, retryCount + 1);
                }

                _logger.LogError("Gemini Vision error: {Status}", response.StatusCode);
                return "{}";
            }

            _logger.LogInformation(
                "Gemini Vision call completed in {Latency}ms", latency);

            var jsonDoc = JsonNode.Parse(responseBody);
            var text = jsonDoc?["candidates"]?[0]?["content"]?["parts"]?[0]
                ?["text"]?.GetValue<string>() ?? "{}";

            return CleanJsonResponse(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini Vision call failed");
            return "{}";
        }
    }

    private static string CleanJsonResponse(string raw)
    {
        var cleaned = raw.Replace("```json", "").Replace("```", "").Trim();
        var start = cleaned.IndexOf('{');
        var end = cleaned.LastIndexOf('}');
        if (start >= 0 && end > start)
            return cleaned[start..(end + 1)];
        return "{}";
    }
}