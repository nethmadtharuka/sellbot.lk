using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SellBotLk.Api.Data;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Models.Entities;
using SellBotLk.Api.Services;

namespace SellBotLk.Api.Webhooks;

[ApiController]
[Route("api/v1/webhook")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly WhatsAppSendService _whatsAppSendService;
    private readonly MessageProcessingService _messageProcessingService;
    private readonly DocumentService _documentService;
    private readonly MediaDownloadService _mediaDownloadService;
    private readonly AppDbContext _context;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IConfiguration config,
        WhatsAppSendService whatsAppSendService,
        MessageProcessingService messageProcessingService,
        DocumentService documentService,
        MediaDownloadService mediaDownloadService,
        AppDbContext context,
        ILogger<WhatsAppWebhookController> logger)
    {
        _config = config;
        _whatsAppSendService = whatsAppSendService;
        _messageProcessingService = messageProcessingService;
        _documentService = documentService;
        _mediaDownloadService = mediaDownloadService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Meta webhook verification — called once when registering the webhook URL.
    /// Returns hub.challenge if the verify token matches.
    /// </summary>
    [HttpGet("whatsapp")]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.verify_token")] string token,
        [FromQuery(Name = "hub.challenge")] string challenge)
    {
        var verifyToken = _config["WhatsApp:VerifyToken"];

        if (mode == "subscribe" && token == verifyToken)
        {
            _logger.LogInformation("WhatsApp webhook verified successfully");
            return Ok(challenge);
        }

        _logger.LogWarning("WhatsApp webhook verification failed — token mismatch");
        return Unauthorized();
    }

    /// <summary>
    /// Receives all incoming WhatsApp messages and events.
    /// HMAC signature verified by HmacVerificationMiddleware before reaching here.
    /// </summary>
    [HttpPost("whatsapp")]
    public async Task<IActionResult> Receive([FromBody] WhatsAppIncomingDto payload)
    {
        try
        {
            var messages = payload.Entry
                .SelectMany(e => e.Changes)
                .Select(c => c.Value)
                .SelectMany(v => v.Messages ?? new List<WhatsAppMessageItemDto>())
                .ToList();

            var contacts = payload.Entry
                .SelectMany(e => e.Changes)
                .Select(c => c.Value)
                .SelectMany(v => v.Contacts ?? new List<WhatsAppContactDto>())
                .ToList();

            foreach (var message in messages)
            {
                var senderName = contacts
                    .FirstOrDefault(c => c.WaId == message.From)
                    ?.Profile.Name ?? "Unknown";

                _logger.LogInformation(
                    "Message received from {Phone} ({Name}) — Type: {Type}",
                    MaskPhone(message.From), senderName, message.Type);

                await ProcessMessageAsync(message, senderName);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook payload");
            return Ok(); // Always return 200 to Meta — never let Meta retry
        }
    }

    private async Task ProcessMessageAsync(
        WhatsAppMessageItemDto message, string senderName)
    {
        switch (message.Type)
        {
            case "text":
                await _messageProcessingService.ProcessTextMessageAsync(
                    message.From,
                    message.Text?.Body ?? "",
                    senderName);
                break;

            case "image":
                var imageMediaId = message.Image?.Id;
                if (!string.IsNullOrEmpty(imageMediaId))
                {
                    await _whatsAppSendService.SendTextMessageAsync(
                        message.From,
                        "📄 Processing your image... please wait.");

                    var (imgBytes, imgMime) = await _mediaDownloadService
                        .DownloadMediaAsync(imageMediaId);

                    if (imgBytes.Length > 0)
                    {
                        var customer = await GetCustomerByPhoneAsync(message.From);

                        // Images from customers treated as payment slips by default.
                        // Sprint 10 will add AI-based document type detection.
                        await _documentService.ProcessDocumentAsync(
                            imgBytes,
                            imgMime,
                            DocumentType.PaymentSlip,
                            customer?.Id,
                            message.From);
                    }
                    else
                    {
                        await _whatsAppSendService.SendTextMessageAsync(
                            message.From,
                            "Sorry, I couldn't download your image. " +
                            "Please try sending it again.");
                    }
                }
                break;

            case "audio":
                _logger.LogInformation("Audio received — Media ID: {Id}",
                    message.Audio?.Id);
                await _whatsAppSendService.SendTextMessageAsync(
                    message.From,
                    "Voice note received! Audio processing coming in Sprint 14.");
                break;

            case "document":
                var docMediaId = message.Document?.Id;
                if (!string.IsNullOrEmpty(docMediaId))
                {
                    await _whatsAppSendService.SendTextMessageAsync(
                        message.From,
                        "📄 Processing your document... please wait.");

                    var (docBytes, docMime) = await _mediaDownloadService
                        .DownloadMediaAsync(docMediaId);

                    if (docBytes.Length > 0)
                    {
                        var customer = await GetCustomerByPhoneAsync(message.From);

                        // PDFs treated as supplier invoices by default.
                        // Owner can reprocess with correct type from dashboard.
                        await _documentService.ProcessDocumentAsync(
                            docBytes,
                            docMime,
                            DocumentType.SupplierInvoice,
                            customer?.Id,
                            message.From);
                    }
                    else
                    {
                        await _whatsAppSendService.SendTextMessageAsync(
                            message.From,
                            "Sorry, I couldn't download your document. " +
                            "Please try again.");
                    }
                }
                break;

            default:
                _logger.LogInformation("Unhandled message type: {Type}",
                    message.Type);
                break;
        }
    }

    private async Task<Customer?> GetCustomerByPhoneAsync(string phone)
    {
        return await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.PhoneNumber == phone);
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 6) return "***";
        return phone[..6] + "***" + phone[^3..];
    }
}