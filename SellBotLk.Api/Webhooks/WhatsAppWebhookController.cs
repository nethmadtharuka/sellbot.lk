using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly PartitionedRateLimiter<string> _senderLimiter;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IConfiguration config,
        WhatsAppSendService whatsAppSendService,
        MessageProcessingService messageProcessingService,
        DocumentService documentService,
        MediaDownloadService mediaDownloadService,
        PaymentMatchingService paymentMatchingService,
        AppDbContext context,
        PartitionedRateLimiter<string> senderLimiter,
        ILogger<WhatsAppWebhookController> logger)
    {
        _senderLimiter = senderLimiter;
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
    /// Each message ID runs at most once. A message that fails while processing gets
    /// a "please try again" reply rather than a retry, so an order can't be repeated.
    /// Only a failure before a message could be claimed returns 500, asking Meta to
    /// redeliver; messages already claimed in that batch are skipped on the retry.
    /// </summary>
    [HttpPost("whatsapp")]
    [EnableRateLimiting("webhook")]
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
                    ?.Profile?.Name ?? "Unknown";

                // Checked first, so a flood costs no database writes or AI calls.
                using var lease = _senderLimiter.AttemptAcquire(message.From);
                if (!lease.IsAcquired)
                {
                    _logger.LogWarning(
                        "Sender rate limit exceeded — dropped message from {Phone}",
                        MaskPhone(message.From));
                    continue;
                }

                if (!await TryClaimMessageAsync(message.Id))
                {
                    _logger.LogInformation(
                        "Duplicate webhook delivery ignored from {Phone}",
                        MaskPhone(message.From));
                    continue;
                }

                _logger.LogInformation(
                    "Message received from {Phone} ({Name}) — Type: {Type}",
                    MaskPhone(message.From), senderName, message.Type);

                // Mark as read + show typing indicator before processing
                await _whatsAppSendService.MarkAsReadWithTypingAsync(message.Id);

                try
                {
                    await ProcessMessageAsync(message, senderName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to process message from {Phone} — Type: {Type}",
                        MaskPhone(message.From), message.Type);

                    // A failed save must not leak into the next message in this batch.
                    _context.ChangeTracker.Clear();

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
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Atomically claims a WhatsApp message ID. The primary key means only one
    /// delivery can win, even if Meta's retries overlap with the original.
    /// </summary>
    private async Task<bool> TryClaimMessageAsync(string messageId)
    {
        var rows = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT IGNORE INTO ProcessedWebhookMessages (MessageId, ProcessedAt) VALUES ({messageId}, {DateTime.UtcNow})");

        return rows == 1;
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

        // Only the owner's number can submit supplier invoices. Meta's signed payload
        // vouches for the sender, and customers' documents can never change stock.
        var isOwner = _documentService.IsOwnerPhone(message.From);
        var customer = await GetCustomerByPhoneAsync(message.From);

        if (!isOwner && !await IsPaymentContextAsync(customer?.Id))
        {
            _logger.LogWarning(
                "Ignored document from non-owner {Phone} with no payment pending",
                MaskPhone(message.From));

            await _whatsAppSendService.SendTextMessageAsync(
                message.From,
                "Thanks, but I can only accept documents as payment slips for an " +
                "open order. Please type your question and I'll help you!");
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

        // Owner documents are treated as supplier invoices; a customer's is a payment slip.
        await _documentService.ProcessDocumentAsync(
            docBytes,
            docMime,
            isOwner ? DocumentType.SupplierInvoice : DocumentType.PaymentSlip,
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