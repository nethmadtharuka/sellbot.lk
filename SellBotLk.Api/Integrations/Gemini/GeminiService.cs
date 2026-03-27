using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using SellBotLk.Api.Models.DTOs;

namespace SellBotLk.Api.Integrations.Gemini;

public class GeminiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<GeminiService> _logger;
    private readonly IMemoryCache _cache;

    private const string BaseUrl =
        "https://generativelanguage.googleapis.com/v1beta/models";

    public GeminiService(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<GeminiService> logger,
        IMemoryCache cache)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Parses an incoming WhatsApp message and returns structured intent,
    /// language, and a reply message — all in one Gemini call.
    /// </summary>
    public async Task<ParsedMessageIntent> ParseMessageAsync(
        string messageText,
        string customerName,
        string conversationContext = "")
    {
        var cacheKey = $"gemini_parse_{messageText.GetHashCode()}";
        if (_cache.TryGetValue(cacheKey, out ParsedMessageIntent? cached))
        {
            _logger.LogInformation("Gemini cache hit for message parse");
            return cached!;
        }

        var prompt = BuildParsePrompt(messageText, customerName, conversationContext);
        var result = await CallGeminiAsync(prompt);
        var parsed = ParseGeminiResponse(result);

        _cache.Set(cacheKey, parsed, TimeSpan.FromHours(1));
        return parsed;
    }

    /// <summary>
    /// Generates an AI response for a given context and instruction.
    /// Used for order confirmations, negotiations, and custom replies.
    /// </summary>
    public async Task<string> GenerateReplyAsync(string instruction, string language = "en")
    {
        var languageName = language switch
        {
            "si" => "Sinhala",
            "ta" => "Tamil",
            _ => "English"
        };

        var prompt = $"""
            You are SellBot, a friendly WhatsApp assistant for a Sri Lankan 
            furniture business. 
            
            {instruction}
            
            IMPORTANT: Reply ONLY in {languageName}. 
            Keep the reply under 200 words.
            Be friendly and professional.
            Do not include any JSON or code — just the message text.
            """;

        return await CallGeminiAsync(prompt);
    }
    /// <summary>
/// Analyzes a furniture image using Gemini Vision and extracts
/// product attributes for visual similarity matching.
/// </summary>
public async Task<VisualSearchAttributes> AnalyzeImageAsync(byte[] imageBytes, string mimeType)
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
                    new
                    {
                        text = """
                            Analyze this furniture image and respond with ONLY a valid JSON object.
                            No markdown, no backticks, no explanation.

                            {
                              "type": "Chair/Table/Sofa/Desk/Bed/Storage/Other",
                              "color": "primary color",
                              "material": "Wood/Fabric/Leather/Metal/Glass/Other",
                              "style": "Modern/Traditional/Scandinavian/Industrial/Classic/Other",
                              "description": "one sentence describing the furniture"
                            }
                            """
                    }
                }
            }
        },
        generationConfig = new
        {
            temperature = 0.1,
            maxOutputTokens = 500
        }
    };

    var json = JsonSerializer.Serialize(requestBody);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    try
    {
        var response = await _httpClient.PostAsync(url, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini Vision error: {Status}", response.StatusCode);
            return new VisualSearchAttributes();
        }

        var jsonDoc = JsonNode.Parse(responseBody);
        var text = jsonDoc?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]
            ?.GetValue<string>() ?? "";

        var cleaned = text.Replace("```json", "").Replace("```", "").Trim();
        var start = cleaned.IndexOf('{');
        var end = cleaned.LastIndexOf('}');
        if (start >= 0 && end > start)
            cleaned = cleaned[start..(end + 1)];

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<VisualSearchAttributes>(cleaned, options)
               ?? new VisualSearchAttributes();
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to analyze image with Gemini Vision");
        return new VisualSearchAttributes();
    }
}

    private string BuildParsePrompt(
    string message, string customerName, string context)
{
    var jsonExample = """
        {
          "intent": "Greeting/ProductSearch/Order/OrderStatus/PriceNegotiation/Complaint/DeliveryInfo/PaymentConfirmation/Other",
          "language": "en or si or ta",
          "customerName": "extracted name if mentioned, else null",
          "orderItems": [{"productName": "name", "quantity": 1, "offeredPrice": null}],
          "productSearchQuery": "search terms if ProductSearch, else null",
          "orderNumber": "ORD-XXXX-XXX if mentioned, else null",
          "offeredPrice": null,
          "productId": null,
          "replyMessage": "your friendly reply in the detected language",
          "confidence": 0.95
        }
        """;

    return $"You are an AI assistant for SellBot.lk, a Sri Lankan furniture business WhatsApp bot.\n\n" +
           $"Analyze this incoming WhatsApp message and respond with ONLY a valid JSON object — no markdown, no backticks, no explanation.\n\n" +
           $"Customer name: {customerName}\n" +
           $"Conversation context: {context}\n" +
           $"Message: \"{message}\"\n\n" +
           $"Detect the language (en=English, si=Sinhala, ta=Tamil).\n\n" +
           $"Respond with exactly this JSON structure:\n{jsonExample}\n\n" +
           $"Rules:\n" +
           $"- For Sinhala messages, replyMessage must be in Sinhala\n" +
           $"- For Tamil messages, replyMessage must be in Tamil\n" +
           $"- For greetings, welcome the customer warmly\n" +
           $"- For orders, confirm what you understood\n" +
           $"- For product search, ask clarifying questions if needed\n" +
           $"- Always be helpful and friendly";
}
    private async Task<string> CallGeminiAsync(string prompt, int retryCount = 0)
    {
        var apiKey = _config["Gemini:ApiKey"];
        var model = _config["Gemini:Model"] ?? "gemini-1.5-flash";
        var url = $"{BaseUrl}/{model}:generateContent?key={apiKey}";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.3,
                maxOutputTokens = 1000
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
                _logger.LogError("Gemini API error: {Status} — {Body}",
                    response.StatusCode, responseBody);

                if (retryCount < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
                    return await CallGeminiAsync(prompt, retryCount + 1);
                }

                return "{\"intent\":\"Other\",\"language\":\"en\"," +
                       "\"replyMessage\":\"Sorry, I am having trouble processing " +
                       "your request. Please try again in a moment.\",\"confidence\":0}";
            }

            _logger.LogInformation("Gemini call completed in {Latency}ms", latency);

            var jsonDoc = JsonNode.Parse(responseBody);
            var text = jsonDoc?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]
                ?.GetValue<string>() ?? "";

            return text;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Gemini API timeout on attempt {Attempt}", retryCount + 1);

            if (retryCount < 2)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
                return await CallGeminiAsync(prompt, retryCount + 1);
            }

            return "{\"intent\":\"Other\",\"language\":\"en\"," +
                   "\"replyMessage\":\"Sorry, I am experiencing a delay. " +
                   "Please try again shortly.\",\"confidence\":0}";
        }
    }

    private ParsedMessageIntent ParseGeminiResponse(string rawResponse)
    {
        try
        {
            var cleaned = rawResponse
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            var start = cleaned.IndexOf('{');
            var end = cleaned.LastIndexOf('}');

            if (start >= 0 && end > start)
                cleaned = cleaned[start..(end + 1)];

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<ParsedMessageIntent>(cleaned, options)
                ?? FallbackIntent();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Gemini response: {Raw}", rawResponse);
            return FallbackIntent();
        }
    }

    private static ParsedMessageIntent FallbackIntent() => new()
    {
        Intent = "Other",
        Language = "en",
        ReplyMessage = "Sorry, I did not understand that. " +
                      "Please try again or type 'help' for assistance.",
        Confidence = 0
    };
}