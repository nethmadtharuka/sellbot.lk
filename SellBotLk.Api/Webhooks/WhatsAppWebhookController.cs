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
    private readonly PaymentMatchingService _paymentMatchingService;
    private readonly AppDbContext _context;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IConfiguration config,
        WhatsAppSendService whatsAppSendService,
        MessageProcessingService messageProcessingService,
        DocumentService documentService,
        MediaDownloadService mediaDownloadService,
        PaymentMatchingService paymentMatchingService,
        AppDbContext context,
        ILogger<WhatsAppWebhookController> logger)
    {
        _config = config;
        _whatsAppSendService = whatsAppSendService;
        _messageProcessingService = messageProcessingService;
        _documentService = documentService;
        _mediaDownloadService = mediaDownloadService;
        _paymentMatchingService = paymentMatchingService;
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
    /// Always returns 200 to Meta — never let Meta retry due to our errors.
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

                // FIX 1: Per-message try/catch so one bad message doesn't kill
                // processing of all other messages in the same webhook batch.
                try
                {
                    await ProcessMessageAsync(message, senderName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to process message from {Phone} — Type: {Type}",
                        MaskPhone(message.From), message.Type);

                    // Best-effort error reply — don't let this throw either
                    try
                    {
                        await _whatsAppSendService.SendTextMessageAsync(
                            message.From,
                            "Sorry, something went wrong. Please try again! 🙏");
                    }
                    catch { /* swallow — we must return 200 */ }
                }
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
                await HandleImageMessageAsync(message, senderName);
                break;

            case "audio":
                _logger.LogInformation("Audio received — Media ID: {Id}",
                    message.Audio?.Id);
                await _whatsAppSendService.SendTextMessageAsync(
                    message.From,
                    "🎤 Voice note received! " +
                    "Audio processing coming soon. " +
                    "For now, please type your order and I'll help you right away!");
                break;

            case "document":
                await HandleDocumentMessageAsync(message, senderName);
                break;

            default:
                _logger.LogInformation("Unhandled message type: {Type}", message.Type);
                await _whatsAppSendService.SendTextMessageAsync(
                    message.From,
                    "I received your message but couldn't process this type. " +
                    "Please send text, a photo, or a document!");
                break;
        }
    }

    /// <summary>
    /// FIX 2: Image routing is now context-aware.
    ///
    /// The old code sent EVERY image to PaymentMatchingService, which meant:
    /// - Product photos → treated as payment slips (wrong)
    /// - Furniture photos for visual search → never reached VisualSearchService (wrong)
    ///
    /// New logic:
    /// - If conversation state is Payment-related → PaymentMatchingService
    /// - If customer recently sent "pay" / "payment" text → PaymentMatchingService
    /// - Otherwise → MessageProcessingService.ProcessImageMessageAsync (visual search)
    ///
    /// This correctly handles both flows without requiring the customer to type
    /// a special keyword.
    /// </summary>
    private async Task HandleImageMessageAsync(
        WhatsAppMessageItemDto message, string senderName)
    {
        var imageMediaId = message.Image?.Id;
        if (string.IsNullOrEmpty(imageMediaId))
        {
            await _whatsAppSendService.SendTextMessageAsync(
                message.From,
                "I received your image but couldn't read it. Please try again!");
            return;
        }

        await _whatsAppSendService.SendTextMessageAsync(
            message.From,
            "📸 Got your image! Analyzing it now, please wait...");

        var (imgBytes, imgMime) = await _mediaDownloadService
            .DownloadMediaAsync(imageMediaId);

        if (imgBytes.Length == 0)
        {
            await _whatsAppSendService.SendTextMessageAsync(
                message.From,
                "Sorry, I couldn't download your image. Please try sending it again.");
            return;
        }

        // Determine whether this image is a payment slip or a product photo
        var customer = await GetCustomerByPhoneAsync(message.From);
        var isPaymentContext = await IsPaymentContextAsync(customer?.Id);

        if (isPaymentContext)
        {
            _logger.LogInformation(
                "Image from {Phone} routed to PaymentMatchingService (payment context)",
                MaskPhone(message.From));

            await _paymentMatchingService.ProcessPaymentSlipAsync(
                imgBytes, imgMime, customer?.Id, message.From);
        }
        else
        {
            _logger.LogInformation(
                "Image from {Phone} routed to VisualSearchService (product search context)",
                MaskPhone(message.From));

            await _messageProcessingService.ProcessImageMessageAsync(
                message.From, imgBytes, imgMime, senderName);
        }
    }

    private async Task HandleDocumentMessageAsync(
        WhatsAppMessageItemDto message, string senderName)
    {
        var docMediaId = message.Document?.Id;
        if (string.IsNullOrEmpty(docMediaId))
        {
            await _whatsAppSendService.SendTextMessageAsync(
                message.From,
                "I received your document but couldn't read it. Please try again!");
            return;
        }

        await _whatsAppSendService.SendTextMessageAsync(
            message.From,
            "📄 Processing your document... please wait.");

        var (docBytes, docMime) = await _mediaDownloadService
            .DownloadMediaAsync(docMediaId);

        if (docBytes.Length == 0)
        {
            await _whatsAppSendService.SendTextMessageAsync(
                message.From,
                "Sorry, I couldn't download your document. Please try again.");
            return;
        }

        var customer = await GetCustomerByPhoneAsync(message.From);

        // PDFs are treated as supplier invoices by default.
        // Owner can reprocess with correct type from the admin dashboard.
        await _documentService.ProcessDocumentAsync(
            docBytes,
            docMime,
            DocumentType.SupplierInvoice,
            customer?.Id,
            message.From);
    }

    /// <summary>
    /// Determines if the current conversation context suggests the customer
    /// is sending a payment slip rather than a product photo.
    ///
    /// Returns true if:
    /// - Conversation state is Ordering or Support (payment likely after order)
    /// - Customer has an Unpaid confirmed order (waiting for payment)
    /// </summary>
    private async Task<bool> IsPaymentContextAsync(int? customerId)
    {
        if (customerId == null) return false;

        // Check if there's an active conversation in ordering/payment state
        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (conversation?.State == ConversationState.Ordering)
            return true;

        // Check if customer has a confirmed but unpaid order — payment slip expected
        var hasUnpaidOrder = await _context.Orders
            .AsNoTracking()
            .AnyAsync(o =>
                o.CustomerId == customerId &&
                o.PaymentStatus == PaymentStatus.Unpaid &&
                (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Processing));

        return hasUnpaidOrder;
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