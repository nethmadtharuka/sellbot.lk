using System.Security.Cryptography;
using System.Text;

namespace SellBotLk.Api.Middleware;

public class HmacVerificationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HmacVerificationMiddleware> _logger;

    public HmacVerificationMiddleware(RequestDelegate next,
        ILogger<HmacVerificationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration config)
    {
        if (context.Request.Method == "POST" &&
            context.Request.Path.StartsWithSegments("/api/v1/webhook"))
        {
            context.Request.EnableBuffering();

            var signature = context.Request.Headers["X-Hub-Signature-256"]
                .FirstOrDefault();

            if (string.IsNullOrEmpty(signature))
            {
                _logger.LogWarning("Webhook request missing HMAC signature");
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Missing signature");
                return;
            }

            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            context.Request.Body.Position = 0;

            // Meta signs the raw POST body with your *App secret* (App Dashboard → App settings → Basic).
            // Common mistakes: using the access token, verify token, or phone number ID here; or a trailing
            // newline in .env — trim below.
            var appSecret = config["WhatsApp:AppSecret"]?.Trim() ?? "";
            if (string.IsNullOrEmpty(appSecret))
            {
                _logger.LogWarning(
                    "Webhook HMAC check failed: WhatsApp:AppSecret is not set. " +
                    "Set it in appsettings, user secrets, or WHATSAPP_APP_SECRET / META_APP_SECRET in .env.");
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("App secret not configured");
                return;
            }

            var expectedSignature = ComputeHmac(body, appSecret);

            if (!signature.Equals($"sha256={expectedSignature}",
                StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Invalid HMAC on webhook: body signature does not match. " +
                    "Confirm WhatsApp:AppSecret is the Meta *App secret* for this app (not the access token).");
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid signature");
                return;
            }
        }

        await _next(context);
    }

    private static string ComputeHmac(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLower();
    }
}