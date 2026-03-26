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
        var phoneNumberId = _config["WhatsApp:PhoneNumberId"];
        var token = _config["WhatsApp:Token"];

        var url = $"https://graph.facebook.com/v22.0/{phoneNumberId}/messages";

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toPhoneNumber,
            type = "text",
            text = new { body = message }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var response = await _httpClient.PostAsync(url, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("WhatsApp send failed: {StatusCode} — {Body}",
                response.StatusCode, responseBody);
        }
        else
        {
            _logger.LogInformation("WhatsApp message sent to {Phone}", 
                MaskPhone(toPhoneNumber));
        }
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 6) return "***";
        return phone[..6] + "***" + phone[^3..];
    }
}