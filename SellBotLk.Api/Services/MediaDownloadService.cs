using System.Text.Json.Nodes;

namespace SellBotLk.Api.Services;

public class MediaDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<MediaDownloadService> _logger;

    public MediaDownloadService(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<MediaDownloadService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Downloads a media file from Meta using the media ID.
    /// Returns the file bytes and MIME type.
    /// </summary>
    public async Task<(byte[] Bytes, string MimeType)> DownloadMediaAsync(string mediaId)
    {
        var token = _config["WhatsApp:Token"];

        // Step 1 — Get the download URL from Meta
        var metaUrl = $"https://graph.facebook.com/v22.0/{mediaId}";

        using var request = new HttpRequestMessage(HttpMethod.Get, metaUrl);
        request.Headers.Add("Authorization", $"Bearer {token}");

        var metaResponse = await _httpClient.SendAsync(request);
        var metaBody = await metaResponse.Content.ReadAsStringAsync();

        if (!metaResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get media URL: {Status} — {Body}",
                metaResponse.StatusCode, metaBody);
            return (Array.Empty<byte>(), "image/jpeg");
        }

        var jsonDoc = JsonNode.Parse(metaBody);
        var downloadUrl = jsonDoc?["url"]?.GetValue<string>();
        var mimeType = jsonDoc?["mime_type"]?.GetValue<string>() ?? "image/jpeg";

        if (string.IsNullOrEmpty(downloadUrl))
        {
            _logger.LogError("No download URL in Meta response: {Body}", metaBody);
            return (Array.Empty<byte>(), mimeType);
        }

        // Step 2 — Download the actual file
        using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        downloadRequest.Headers.Add("Authorization", $"Bearer {token}");

        var downloadResponse = await _httpClient.SendAsync(downloadRequest);

        if (!downloadResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to download media file: {Status}",
                downloadResponse.StatusCode);
            return (Array.Empty<byte>(), mimeType);
        }

        var bytes = await downloadResponse.Content.ReadAsByteArrayAsync();

        _logger.LogInformation(
            "Media downloaded successfully — {Bytes} bytes, {MimeType}",
            bytes.Length, mimeType);

        return (bytes, mimeType);
    }
     public static bool IsAcceptedType(string? mimeType)
    {
        if (string.IsNullOrEmpty(mimeType)) return false;

        var accepted = new[]
        {
            "image/jpeg", "image/jpg", "image/png", "image/webp",
            "application/pdf"
        };

        return accepted.Contains(mimeType.ToLower());
    }

}