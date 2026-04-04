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
    /// Now includes the product catalogue so Gemini can match products accurately.
    /// </summary>
    public async Task<ParsedMessageIntent> ParseMessageAsync(
        string messageText,
        string customerName,
        string conversationContext = "",
        string catalogueSummary = "")
    {
        // FIX 1: Cache key must include customerName so "hello" from different
        // customers doesn't return the same cached reply with the wrong name.
        // Also include first 50 chars of context so mid-conversation messages
        // don't get stale cached responses.
        var cacheKey = $"gemini_parse_{customerName}_{messageText.GetHashCode()}" +
                       $"_{conversationContext[..Math.Min(50, conversationContext.Length)]}";

        if (_cache.TryGetValue(cacheKey, out ParsedMessageIntent? cached))
        {
            _logger.LogInformation("Gemini cache hit for message parse");
            return cached!;
        }

        var prompt = BuildParsePrompt(
            messageText, customerName, conversationContext, catalogueSummary);

        var result = await CallGeminiAsync(prompt);
        var parsed = ParseGeminiResponse(result);

        // FIX 2: Don't cache failed/fallback responses — only cache confident results
        if (parsed.Confidence > 0.3)
            _cache.Set(cacheKey, parsed, TimeSpan.FromMinutes(10));

        return parsed;
    }

    /// <summary>
    /// Generates a plain text AI reply for a given instruction.
    /// Used for order confirmations, negotiations, and custom replies.
    /// NOTE: Do NOT use this to generate JSON — use CallGeminiRawAsync instead.
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
    /// Calls Gemini for tasks that need raw JSON output (like SmartSearch).
    /// Does NOT wrap the prompt in any system persona — returns exactly what
    /// the prompt asks for without "Be friendly" interference.
    /// </summary>
    public async Task<string> CallGeminiRawAsync(string prompt)
    {
        return await CallGeminiAsync(prompt);
    }

    /// <summary>
    /// Analyzes a furniture image using Gemini Vision and extracts
    /// product attributes for visual similarity matching.
    /// </summary>
    public async Task<VisualSearchAttributes> AnalyzeImageAsync(
        byte[] imageBytes, string mimeType)
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

            // FIX 3: Check for safety blocks before reading text
            var finishReason = jsonDoc?["candidates"]?[0]?["finishReason"]
                ?.GetValue<string>() ?? "";
            if (finishReason is "SAFETY" or "RECITATION")
            {
                _logger.LogWarning("Gemini Vision blocked response: {Reason}", finishReason);
                return new VisualSearchAttributes();
            }

            var text = jsonDoc?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]
                ?.GetValue<string>() ?? "";

            var cleaned = CleanJsonResponse(text);
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
        string message,
        string customerName,
        string context,
        string catalogueSummary)
    {
        // FIX 4: Inject the actual product catalogue into the prompt so Gemini
        // can accurately extract product names and match them to real products.
        // Without this, Gemini guesses product names from thin air.
        var catalogueSection = string.IsNullOrEmpty(catalogueSummary)
            ? ""
            : $"\nAvailable products (use these exact names in orderItems):\n{catalogueSummary}\n";

        var contextSection = string.IsNullOrEmpty(context)
            ? ""
            : $"\nConversation context: {context}\n";

        var jsonExample = """
            {
              "intent": "Greeting/ProductSearch/Order/OrderStatus/PriceNegotiation/Complaint/DeliveryInfo/PaymentConfirmation/Other",
              "language": "en or si or ta",
              "customerName": "extracted name if mentioned, else null",
              "orderItems": [{"productName": "exact name from catalogue", "quantity": 1, "offeredPrice": null}],
              "productSearchQuery": "search terms if ProductSearch, else null",
              "orderNumber": "ORD-XXXX-XXX if mentioned, else null",
              "offeredPrice": null,
              "productId": null,
              "replyMessage": "your friendly reply in the detected language",
              "confidence": 0.95
            }
            """;

        return $"You are an AI assistant for SellBot.lk, a Sri Lankan furniture business WhatsApp bot.\n\n" +
               $"Analyze this incoming WhatsApp message and respond with ONLY a valid JSON object.\n" +
               $"No markdown, no backticks, no explanation — ONLY the JSON.\n\n" +
               $"Customer name: {customerName}\n" +
               catalogueSection +
               contextSection +
               $"Message: \"{message}\"\n\n" +
               $"Detect the language (en=English, si=Sinhala, ta=Tamil).\n\n" +
               $"Respond with exactly this JSON structure:\n{jsonExample}\n\n" +
               // FIX 5: Explicit rules for mixed-language messages (Sinhala+English product names)
               $"Rules:\n" +
               $"- Customers may mix languages — e.g. 'chair ekak denna' is an Order intent in Sinhala\n" +
               $"- 'දෙන්න', 'ගන්න', 'order', 'want', 'need', 'buy' all indicate Order intent\n" +
               $"- NEVER return intent=Other for a message that clearly wants a product or order\n" +
               $"- For OrderStatus, set orderNumber if the customer mentions ORD-XXXX\n" +
               $"- replyMessage must be in the detected language\n" +
               $"- For Sinhala: replyMessage must be in Sinhala script\n" +
               $"- For Tamil: replyMessage must be in Tamil script\n" +
               $"- For greetings, welcome the customer warmly and mention you can help with products and orders\n" +
               $"- Be helpful and friendly — if intent is unclear, ask a clarifying question in replyMessage";
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

                return BuildFallbackJson();
            }

            _logger.LogInformation("Gemini call completed in {Latency}ms", latency);

            var jsonDoc = JsonNode.Parse(responseBody);

            // FIX 3: Check candidates array exists and is not empty
            var candidates = jsonDoc?["candidates"];
            if (candidates == null || candidates.AsArray().Count == 0)
            {
                _logger.LogWarning("Gemini returned empty candidates array");
                return BuildFallbackJson();
            }

            // FIX 3: Check for safety/recitation blocks — these are silent failures
            var finishReason = candidates[0]?["finishReason"]?.GetValue<string>() ?? "";
            if (finishReason is "SAFETY" or "RECITATION")
            {
                _logger.LogWarning("Gemini blocked response — finishReason: {Reason}", finishReason);
                return BuildFallbackJson();
            }

            var text = candidates[0]?["content"]?["parts"]?[0]?["text"]
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

            return BuildFallbackJson("Sorry, I am experiencing a delay. Please try again shortly.");
        }
    }

    private ParsedMessageIntent ParseGeminiResponse(string rawResponse)
    {
        try
        {
            var cleaned = CleanJsonResponse(rawResponse);

            if (string.IsNullOrWhiteSpace(cleaned))
                return FallbackIntent();

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

    private static string CleanJsonResponse(string raw)
    {
        var cleaned = raw
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

        var start = cleaned.IndexOf('{');
        var end = cleaned.LastIndexOf('}');

        if (start >= 0 && end > start)
            return cleaned[start..(end + 1)];

        return cleaned;
    }

    // FIX 6: Single place to build fallback JSON — consistent across all error paths
    private static string BuildFallbackJson(string? message = null) =>
        "{\"intent\":\"Other\",\"language\":\"en\"," +
        $"\"replyMessage\":\"{message ?? "Sorry, I am having trouble right now. Please try again in a moment."}\",\"confidence\":0}}";

    private static ParsedMessageIntent FallbackIntent() => new()
    {
        Intent = "Other",
        Language = "en",
        ReplyMessage = "Sorry, I didn't understand that. " +
                       "You can browse products, place an order, or type 'help'.",
        Confidence = 0
    };
}