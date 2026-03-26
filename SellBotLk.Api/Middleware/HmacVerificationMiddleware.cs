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

            var appSecret = config["WhatsApp:AppSecret"] ?? "";
            var expectedSignature = ComputeHmac(body, appSecret);

            if (!signature.Equals($"sha256={expectedSignature}",
                StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid HMAC signature on webhook request");
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