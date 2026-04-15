using System.Text;
using System.Text.Json;

namespace SellBotLk.Api.Services;

public class WhatsAppSendService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppSendService> _logger;

    public WhatsAppSendService(HttpClient httpClient,
        IConfiguration config,
        ILogger<WhatsAppSendService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task SendTextMessageAsync(string toPhoneNumber, string message)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toPhoneNumber,
            type = "text",
            text = new { body = message }
        };

        await PostToWhatsAppAsync(payload);
    }

    /// <summary>
    /// Sends an interactive message with up to 3 quick-reply buttons.
    /// Each button is a tuple of (id, title) where title is max 20 chars.
    /// </summary>
    public async Task SendButtonMessageAsync(
        string toPhoneNumber,
        string bodyText,
        IEnumerable<(string Id, string Title)> buttons)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toPhoneNumber,
            type = "interactive",
            interactive = new
            {
                type = "button",
                body = new { text = bodyText },
                action = new
                {
                    buttons = buttons.Take(3).Select(b => new
                    {
                        type = "reply",
                        reply = new { id = b.Id, title = b.Title }
                    }).ToArray()
                }
            }
        };

        await PostToWhatsAppAsync(payload);
    }

    /// <summary>
    /// Marks an inbound message as read and shows a "typing..." indicator.
    /// Must be called with the incoming message's wamid before replying.
    /// </summary>
    public async Task MarkAsReadWithTypingAsync(string messageId)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            status = "read",
            message_id = messageId,
            typing_indicator = new { type = "text" }
        };

        try
        {
            await PostToWhatsAppAsync(payload, logFailure: false);
        }
        catch
        {
            // Best-effort — never block a real reply
        }
    }

    /// <summary>
    /// Sends an image message using a publicly accessible URL.
    /// </summary>
    public async Task SendImageMessageAsync(string toPhoneNumber, string imageUrl, string? caption = null)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toPhoneNumber,
            type = "image",
            image = new { link = imageUrl, caption = caption ?? "" }
        };

        await PostToWhatsAppAsync(payload);
    }

    /// <summary>
    /// Uploads a document (PDF) to Meta's media endpoint then sends it to the user.
    /// </summary>
    public async Task SendDocumentMessageAsync(
        string toPhoneNumber, byte[] fileBytes, string filename, string? caption = null)
    {
        var phoneNumberId = _config["WhatsApp:PhoneNumberId"];
        var token = _config["WhatsApp:Token"];

        var uploadUrl = $"https://graph.facebook.com/v22.0/{phoneNumberId}/media";

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", filename);
        form.Add(new StringContent("application/pdf"), "type");
        form.Add(new StringContent("whatsapp"), "messaging_product");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var uploadResponse = await _httpClient.PostAsync(uploadUrl, form);
        var uploadBody = await uploadResponse.Content.ReadAsStringAsync();

        if (!uploadResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Media upload failed: {Status} — {Body}",
                uploadResponse.StatusCode, uploadBody);
            return;
        }

        var mediaId = System.Text.Json.JsonDocument.Parse(uploadBody)
            .RootElement.GetProperty("id").GetString();

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toPhoneNumber,
            type = "document",
            document = new { id = mediaId, filename, caption = caption ?? "" }
        };

        await PostToWhatsAppAsync(payload);
    }

    private async Task PostToWhatsAppAsync(object payload, bool logFailure = true)
    {
        var phoneNumberId = _config["WhatsApp:PhoneNumberId"];
        var token = _config["WhatsApp:Token"];
        var url = $"https://graph.facebook.com/v22.0/{phoneNumberId}/messages";

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var response = await _httpClient.PostAsync(url, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode && logFailure)
        {
            _logger.LogError("WhatsApp send failed: {StatusCode} — {Body}",
                response.StatusCode, responseBody);
        }
        else if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("WhatsApp message sent successfully");
        }
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 6) return "***";
        return phone[..6] + "***" + phone[^3..];
    }
}