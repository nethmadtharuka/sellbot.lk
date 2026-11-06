using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SellBotLk.Api.Data;
using SellBotLk.Api.Integrations.Gemini;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Models.Entities;
using System.Security.Cryptography;
using System.Text.Json;

namespace SellBotLk.Api.Services;

public class PaymentMatchingService
{
    private const int MaxSlipBytes = 10 * 1024 * 1024;
    private const int MaxReferenceLength = 100;

    // Placeholders an AI may return when a slip shows no reference.
    private static readonly HashSet<string> NoReferenceValues =
        new() { "NA", "NONE", "NULL", "UNKNOWN" };

    private readonly AppDbContext _db;
    private readonly GeminiVisionService _visionService;
    private readonly WhatsAppSendService _whatsAppSendService;
    private readonly DeliveryService _deliveryService;
    private readonly InvoiceService _invoiceService;
    private readonly OrderService _orderService;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentMatchingService> _logger;

    public PaymentMatchingService(
        AppDbContext db,
        GeminiVisionService visionService,
        WhatsAppSendService whatsAppSendService,
        DeliveryService deliveryService,
        InvoiceService invoiceService,
        OrderService orderService,
        IConfiguration config,
        ILogger<PaymentMatchingService> logger)
    {
        _db = db;
        _visionService = visionService;
        _whatsAppSendService = whatsAppSendService;
        _deliveryService = deliveryService;
        _invoiceService = invoiceService;
        _orderService = orderService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Handles a submitted payment slip:
    /// 1. Extract data from the image via Gemini Vision
    /// 2. Match it to the customer's unpaid order
    /// 3. Check the amount, and claim the slip so it can't be reused
    /// 4. Queue the order for admin review
    ///
    /// AI output only decides which order a human looks at. It can never mark an
    /// order Paid: a screenshot isn't proof of payment. See ApprovePaymentAsync.
    /// fromPhone is null for admin uploads, which get no WhatsApp replies.
    /// </summary>
    public async Task<PaymentVerificationResultDto> ProcessPaymentSlipAsync(
        byte[] imageBytes,
        string mimeType,
        int? customerId,
        string? fromPhone)
    {
        var document = new Document
        {
            Type = DocumentType.PaymentSlip,
            CustomerId = customerId,
            ExtractedData = "{}",
            CreatedAt = DateTime.UtcNow
        };

        // Saved up front, outside the claim's transaction, so a rolled-back claim
        // can't take this audit record with it.
        _db.Documents.Add(document);
        await _db.SaveChangesAsync();

        PaymentVerificationResultDto result;

        if (imageBytes.Length > MaxSlipBytes || !MediaDownloadService.IsAcceptedType(mimeType))
        {
            await SendAsync(fromPhone,
                "⚠️ Please send your payment slip as a JPEG, PNG or PDF under 10MB.");

            result = new PaymentVerificationResultDto
            {
                Message = "Rejected: unsupported file type or size"
            };
        }
        else
        {
            var extractedJson = await _visionService
                .ExtractPaymentDataAsync(imageBytes, mimeType);

            ExtractedPaymentData? payment;
            try
            {
                payment = JsonSerializer.Deserialize<ExtractedPaymentData>(
                    extractedJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                document.ExtractedData = extractedJson;
            }
            catch
            {
                payment = null;
            }

            document.TotalAmount = payment?.Amount;
            await _db.SaveChangesAsync();

            result = await QueueForReviewAsync(
                payment, imageBytes, mimeType, customerId, fromPhone);
        }

        document.IsProcessed = result.Success;
        document.ProcessingNotes = result.Message;
        await _db.SaveChangesAsync();

        result.DocumentId = document.Id;
        return result;
    }

    private async Task<PaymentVerificationResultDto> QueueForReviewAsync(
        ExtractedPaymentData? payment,
        byte[] imageBytes,
        string mimeType,
        int? customerId,
        string? fromPhone)
    {
        if (payment == null || payment.Confidence < 0.5)
        {
            await SendAsync(fromPhone,
                "⚠️ I had trouble reading your payment slip.\n\n" +
                "Please send a clearer photo:\n" +
                "• Good lighting\n" +
                "• All text visible\n" +
                "• No blurring\n\n" +
                "Your payment has NOT been confirmed yet.");

            return new PaymentVerificationResultDto
            {
                Message = $"Low confidence extraction: {payment?.Confidence ?? 0:P0}"
            };
        }

        if (!payment.Amount.HasValue || payment.Amount <= 0)
        {
            await SendAsync(fromPhone,
                "⚠️ I could read your slip but couldn't extract the amount. " +
                "Please send a clearer photo or contact us directly.");

            return new PaymentVerificationResultDto { Message = "Amount not extracted" };
        }

        _logger.LogInformation(
            "Payment extracted — Amount: {Amount}, Ref: {Ref}, Bank: {Bank}, " +
            "Confidence: {Conf:P0}",
            payment.Amount, payment.Reference, payment.Bank, payment.Confidence);

        // Find matching unpaid order
        var order = await FindMatchingOrderAsync(customerId, payment.Amount.Value);

        if (order == null)
        {
            await SendAsync(fromPhone,
                $"✅ Payment slip received!\n\n" +
                $"Amount: LKR {payment.Amount:N0}\n" +
                $"Reference: {payment.Reference ?? "N/A"}\n" +
                $"Bank: {payment.Bank ?? "N/A"}\n\n" +
                $"⚠️ However, I couldn't find a pending order matching " +
                $"this amount. Please contact us so we can match it manually.");

            await AlertOwnerAsync(
                $"⚠️ Unmatched payment received!\n" +
                $"From: {fromPhone ?? "admin upload"}\n" +
                $"Amount: LKR {payment.Amount:N0}\n" +
                $"Ref: {payment.Reference ?? "N/A"}\n" +
                $"Bank: {payment.Bank ?? "N/A"}");

            return new PaymentVerificationResultDto
            {
                AmountVerified = payment.Amount,
                Reference = payment.Reference,
                Message = "No matching order found"
            };
        }

        // Verify amount matches (1% tolerance)
        var tolerance = order.TotalAmount * 0.01m;
        var difference = Math.Abs(payment.Amount.Value - order.TotalAmount);

        if (difference > tolerance)
        {
            await SendAsync(fromPhone,
                $"⚠️ Payment amount mismatch!\n\n" +
                $"Order {order.OrderNumber} total: LKR {order.TotalAmount:N0}\n" +
                $"Payment received: LKR {payment.Amount:N0}\n" +
                $"Difference: LKR {difference:N0}\n\n" +
                $"Please contact us to resolve this.");

            await AlertOwnerAsync(
                $"⚠️ Payment mismatch on {order.OrderNumber}!\n" +
                $"Expected: LKR {order.TotalAmount:N0}\n" +
                $"Received: LKR {payment.Amount:N0}\n" +
                $"Diff: LKR {difference:N0}\n" +
                $"Ref: {payment.Reference ?? "N/A"}");

            return new PaymentVerificationResultDto
            {
                OrderNumber = order.OrderNumber,
                AmountVerified = payment.Amount,
                Reference = payment.Reference,
                Message = $"Amount mismatch: LKR {difference:N0} difference"
            };
        }

        var reference = NormalizeReference(payment.Reference);
        var claim = await TryClaimSlipAsync(order.Id, imageBytes, mimeType, reference);

        if (claim == SlipClaim.Duplicate)
        {
            _logger.LogWarning(
                "Duplicate payment slip rejected for order {OrderNumber} — Ref: {Ref}",
                order.OrderNumber, reference);

            await SendAsync(fromPhone,
                "⚠️ This payment slip has already been submitted, so it can't be " +
                "used for this order. Your payment has NOT been confirmed.\n\n" +
                "If you made a new transfer, please send that slip or contact us.");

            await AlertOwnerAsync(
                $"🚨 Reused payment slip!\n" +
                $"Order: {order.OrderNumber}\n" +
                $"From: {fromPhone ?? "admin upload"}\n" +
                $"Ref: {reference ?? "N/A"}");

            return new PaymentVerificationResultDto
            {
                OrderNumber = order.OrderNumber,
                AmountVerified = payment.Amount,
                Reference = reference,
                Message = "Duplicate slip or reference"
            };
        }

        if (claim == SlipClaim.OrderNotAwaitingPayment)
        {
            await SendAsync(fromPhone,
                $"Order {order.OrderNumber} isn't waiting for a payment slip any more. " +
                "Please contact us if you need help.");

            return new PaymentVerificationResultDto
            {
                OrderNumber = order.OrderNumber,
                Message = "Order no longer awaiting payment"
            };
        }

        _logger.LogInformation(
            "Payment slip queued for review — {OrderNumber}, LKR {Amount}",
            order.OrderNumber, payment.Amount);

        await SendAsync(fromPhone,
            $"📄 Payment slip received, thank you!\n\n" +
            $"Order: {order.OrderNumber}\n" +
            $"Amount: LKR {payment.Amount:N0}\n" +
            $"Reference: {reference ?? "N/A"}\n\n" +
            $"Our team will verify your payment and confirm shortly. 🙏");

        await AlertOwnerAsync(
            $"🔎 Payment slip needs review\n" +
            $"Order: {order.OrderNumber}\n" +
            $"Amount: LKR {payment.Amount:N0}\n" +
            $"Ref: {reference ?? "N/A"}\n" +
            $"Customer: {fromPhone ?? "admin upload"}\n\n" +
            $"Check your bank account, then approve or reject it in the dashboard.");

        return new PaymentVerificationResultDto
        {
            Success = true,
            OrderNumber = order.OrderNumber,
            AmountVerified = payment.Amount,
            Reference = reference,
            Message = $"Awaiting admin review for {order.OrderNumber}"
        };
    }

    /// <summary>
    /// Admin decision: the money has been seen, so the order becomes Paid.
    /// This is the only place an order is marked Paid. Returns null if no such order.
    /// </summary>
    public async Task<OrderResponseDto?> ApprovePaymentAsync(int orderId, string? approvedBy)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.Orders
            .Where(o => o.Id == orderId &&
                        o.PaymentStatus == PaymentStatus.PendingReview &&
                        o.Status != OrderStatus.Cancelled)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.PaymentStatus, PaymentStatus.Paid)
                .SetProperty(o => o.UpdatedAt, now));

        var order = await _orderService.GetByIdAsync(orderId);
        if (order == null) return null;

        if (rows == 0)
            throw new Exception("This order has no active payment slip awaiting review.");

        _logger.LogInformation(
            "Payment approved for {OrderNumber} by {Admin}", order.OrderNumber, approvedBy);

        await NotifyPaymentApprovedAsync(order);
        return order;
    }

    /// <summary>
    /// Admin decision: the slip isn't valid. The order goes back to Unpaid and the
    /// slip and reference are released, so a misread slip can be sent again.
    /// Returns null if no such order.
    /// </summary>
    public async Task<OrderResponseDto?> RejectPaymentAsync(
        int orderId, string? rejectedBy, string? reason)
    {
        var now = DateTime.UtcNow;

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var rows = await _db.Orders
            .Where(o => o.Id == orderId && o.PaymentStatus == PaymentStatus.PendingReview)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.PaymentStatus, PaymentStatus.Unpaid)
                .SetProperty(o => o.PaymentReference, (string?)null)
                .SetProperty(o => o.UpdatedAt, now));

        if (rows == 1)
            await _db.PaymentSlips.Where(s => s.OrderId == orderId).ExecuteDeleteAsync();

        await transaction.CommitAsync();

        var order = await _orderService.GetByIdAsync(orderId);
        if (order == null) return null;

        if (rows == 0)
            throw new Exception("This order has no payment slip awaiting review.");

        _logger.LogInformation(
            "Payment rejected for {OrderNumber} by {Admin}: {Reason}",
            order.OrderNumber, rejectedBy, reason);

        await TrySendAsync(order.CustomerPhone,
            $"⚠️ We couldn't verify your payment for order {order.OrderNumber}." +
            (string.IsNullOrWhiteSpace(reason) ? "" : $"\nReason: {reason}") +
            "\n\nPlease check the transfer and send your slip again, or contact us.");

        return order;
    }

    /// <summary>Returns the slip awaiting or backing an order's payment, if any.</summary>
    public Task<PaymentSlip?> GetSlipAsync(int orderId) =>
        _db.PaymentSlips
            .AsNoTracking()
            .Where(s => s.OrderId == orderId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

    /// <summary>
    /// Records the slip against the order in one transaction. Unique indexes on the
    /// image hash and the reference decide the winner, so reuse is caught even when
    /// two submissions arrive at the same moment.
    /// </summary>
    private async Task<SlipClaim> TryClaimSlipAsync(
        int orderId, byte[] imageBytes, string mimeType, string? reference)
    {
        var slip = new PaymentSlip
        {
            OrderId = orderId,
            ImageSha256 = Convert.ToHexString(SHA256.HashData(imageBytes)),
            ImageBytes = imageBytes,
            MimeType = mimeType.ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow
        };

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.PaymentSlips.Add(slip);
            await _db.SaveChangesAsync();

            var now = DateTime.UtcNow;
            var rows = await _db.Orders
                .Where(o => o.Id == orderId &&
                            o.PaymentStatus == PaymentStatus.Unpaid &&
                            o.Status != OrderStatus.Cancelled)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.PaymentStatus, PaymentStatus.PendingReview)
                    .SetProperty(o => o.PaymentReference, reference)
                    .SetProperty(o => o.UpdatedAt, now));

            if (rows == 0)
            {
                await transaction.RollbackAsync();
                _db.Entry(slip).State = EntityState.Detached;
                return SlipClaim.OrderNotAwaitingPayment;
            }

            await transaction.CommitAsync();
            return SlipClaim.Claimed;
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            await transaction.RollbackAsync();
            _db.Entry(slip).State = EntityState.Detached;
            return SlipClaim.Duplicate;
        }
    }

    private static bool IsDuplicateKey(Exception ex) =>
        ex.GetBaseException() is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry };

    /// <summary>
    /// Reduces a reference to upper-case letters and digits, so "txn-123" and
    /// "TXN 123" count as the same. Returns null when there's no usable reference.
    /// </summary>
    public static string? NormalizeReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;

        var normalized = new string(reference.Where(char.IsLetterOrDigit).ToArray())
            .ToUpperInvariant();

        if (normalized.Length == 0 || NoReferenceValues.Contains(normalized)) return null;

        return normalized.Length > MaxReferenceLength
            ? normalized[..MaxReferenceLength]
            : normalized;
    }

    private async Task NotifyPaymentApprovedAsync(OrderResponseDto order)
    {
        // The decision is already saved, so a messaging failure must not fail it.
        try
        {
            var deliveryMessage = "";
            if (!string.IsNullOrEmpty(order.DeliveryArea))
            {
                var delivery = await _deliveryService.CheckZoneAsync(
                    order.DeliveryArea, order.TotalAmount);

                deliveryMessage = delivery.IsServiceable
                    ? $"\n\n🚚 Delivery details:\n{delivery.Message}"
                    : $"\n\n🚚 Delivery to {order.DeliveryArea} will be confirmed shortly.";
            }

            await _whatsAppSendService.SendTextMessageAsync(order.CustomerPhone,
                $"✅ Payment confirmed!\n\n" +
                $"Order: {order.OrderNumber}\n" +
                $"Amount: LKR {order.TotalAmount:N0}\n" +
                $"Reference: {order.PaymentReference ?? "N/A"}" +
                deliveryMessage +
                $"\n\nThank you! We'll start processing your order now. 🙏");

            var pdfBytes = _invoiceService.GenerateInvoicePdf(order, order.PaymentReference);
            await _whatsAppSendService.SendDocumentMessageAsync(
                order.CustomerPhone,
                pdfBytes,
                $"SellBotLk-{order.OrderNumber}.pdf",
                $"Your invoice for order {order.OrderNumber}.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Payment approved but customer notification failed for {OrderNumber}",
                order.OrderNumber);
        }
    }

    private async Task<Order?> FindMatchingOrderAsync(
        int? customerId, decimal amount)
    {
        if (customerId == null) return null;

        // Try exact amount match first
        var exactMatch = await _db.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId &&
                        o.PaymentStatus == PaymentStatus.Unpaid &&
                        o.Status != OrderStatus.Cancelled &&
                        o.Status != OrderStatus.FraudPending)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(o =>
                Math.Abs(o.TotalAmount - amount) <= o.TotalAmount * 0.01m);

        if (exactMatch != null) return exactMatch;

        // Fall back to most recent unpaid order
        return await _db.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId &&
                        o.PaymentStatus == PaymentStatus.Unpaid &&
                        o.Status != OrderStatus.Cancelled &&
                        o.Status != OrderStatus.FraudPending)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();
    }

    private async Task SendAsync(string? phone, string message)
    {
        if (!string.IsNullOrEmpty(phone))
            await _whatsAppSendService.SendTextMessageAsync(phone, message);
    }

    private async Task TrySendAsync(string phone, string message)
    {
        try
        {
            await _whatsAppSendService.SendTextMessageAsync(phone, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send a payment update to the customer");
        }
    }

    private async Task AlertOwnerAsync(string message)
    {
        var ownerPhone = _config["Owner:Phone"] ?? _config["OWNER_PHONE"];
        if (!string.IsNullOrEmpty(ownerPhone))
            await _whatsAppSendService.SendTextMessageAsync(ownerPhone, message);
    }

    private enum SlipClaim
    {
        Claimed,
        Duplicate,
        OrderNotAwaitingPayment
    }
}
