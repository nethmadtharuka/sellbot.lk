using Microsoft.EntityFrameworkCore;
using SellBotLk.Api.Data;
using SellBotLk.Api.Integrations.Gemini;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Models.Entities;
using System.Text.Json;

namespace SellBotLk.Api.Services;

public class PaymentMatchingService
{
    private readonly AppDbContext _db;
    private readonly GeminiVisionService _visionService;
    private readonly WhatsAppSendService _whatsAppSendService;
    private readonly DeliveryService _deliveryService;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentMatchingService> _logger;

    public PaymentMatchingService(
        AppDbContext db,
        GeminiVisionService visionService,
        WhatsAppSendService whatsAppSendService,
        DeliveryService deliveryService,
        IConfiguration config,
        ILogger<PaymentMatchingService> logger)
    {
        _db = db;
        _visionService = visionService;
        _whatsAppSendService = whatsAppSendService;
        _deliveryService = deliveryService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Full payment slip processing pipeline:
    /// 1. Extract data from image via Gemini Vision
    /// 2. Match to pending order
    /// 3. Verify amount
    /// 4. Confirm payment and update order status
    /// 5. Calculate delivery fee and notify customer
    /// </summary>
    public async Task<PaymentVerificationResultDto> ProcessPaymentSlipAsync(
        byte[] imageBytes,
        string mimeType,
        int? customerId,
        string fromPhone)
    {
        // Step 1 — Extract payment data from image
        var extractedJson = await _visionService
            .ExtractPaymentDataAsync(imageBytes, mimeType);

        ExtractedPaymentData? payment;
        try
        {
            payment = JsonSerializer.Deserialize<ExtractedPaymentData>(
                extractedJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            payment = null;
        }

        if (payment == null || payment.Confidence < 0.5)
        {
            var lowConfMsg =
                "⚠️ I had trouble reading your payment slip.\n\n" +
                "Please send a clearer photo:\n" +
                "• Good lighting\n" +
                "• All text visible\n" +
                "• No blurring\n\n" +
                "Your payment has NOT been confirmed yet.";

            await _whatsAppSendService.SendTextMessageAsync(fromPhone, lowConfMsg);

            return new PaymentVerificationResultDto
            {
                Success = false,
                Message = $"Low confidence extraction: " +
                $"{(payment?.Confidence ?? 0):P0}"            };
        }

        if (!payment.Amount.HasValue || payment.Amount <= 0)
        {
            await _whatsAppSendService.SendTextMessageAsync(
                fromPhone,
                "⚠️ I could read your slip but couldn't extract the amount. " +
                "Please send a clearer photo or contact us directly.");

            return new PaymentVerificationResultDto
            {
                Success = false,
                Message = "Amount not extracted"
            };
        }

        _logger.LogInformation(
            "Payment extracted — Amount: {Amount}, Ref: {Ref}, Bank: {Bank}, " +
            "Confidence: {Conf:P0}",
            payment.Amount, payment.Reference, payment.Bank, payment.Confidence);

        // Step 2 — Find matching unpaid order
        var order = await FindMatchingOrderAsync(customerId, payment.Amount.Value);

        if (order == null)
        {
            var noMatchMsg =
                $"✅ Payment slip received!\n\n" +
                $"Amount: LKR {payment.Amount:N0}\n" +
                $"Reference: {payment.Reference ?? "N/A"}\n" +
                $"Bank: {payment.Bank ?? "N/A"}\n\n" +
                $"⚠️ However, I couldn't find a pending order matching " +
                $"this amount. Please contact us so we can match it manually.";

            await _whatsAppSendService.SendTextMessageAsync(fromPhone, noMatchMsg);

            // Alert owner
            await AlertOwnerAsync(
                $"⚠️ Unmatched payment received!\n" +
                $"From: {fromPhone}\n" +
                $"Amount: LKR {payment.Amount:N0}\n" +
                $"Ref: {payment.Reference ?? "N/A"}\n" +
                $"Bank: {payment.Bank ?? "N/A"}");

            return new PaymentVerificationResultDto
            {
                Success = false,
                AmountVerified = payment.Amount,
                Reference = payment.Reference,
                Message = "No matching order found"
            };
        }

        // Step 3 — Verify amount matches (1% tolerance)
        var tolerance = order.TotalAmount * 0.01m;
        var difference = Math.Abs(payment.Amount.Value - order.TotalAmount);

        if (difference > tolerance)
        {
            var mismatchMsg =
                $"⚠️ Payment amount mismatch!\n\n" +
                $"Order {order.OrderNumber} total: LKR {order.TotalAmount:N0}\n" +
                $"Payment received: LKR {payment.Amount:N0}\n" +
                $"Difference: LKR {difference:N0}\n\n" +
                $"Please contact us to resolve this.";

            await _whatsAppSendService.SendTextMessageAsync(fromPhone, mismatchMsg);

            await AlertOwnerAsync(
                $"⚠️ Payment mismatch on {order.OrderNumber}!\n" +
                $"Expected: LKR {order.TotalAmount:N0}\n" +
                $"Received: LKR {payment.Amount:N0}\n" +
                $"Diff: LKR {difference:N0}\n" +
                $"Ref: {payment.Reference ?? "N/A"}");

            return new PaymentVerificationResultDto
            {
                Success = false,
                OrderNumber = order.OrderNumber,
                AmountVerified = payment.Amount,
                Reference = payment.Reference,
                Message = $"Amount mismatch: LKR {difference:N0} difference"
            };
        }

        // Step 4 — Confirm payment and update order
        order.PaymentStatus = PaymentStatus.Paid;
        order.PaymentReference = payment.Reference;
        order.Status = OrderStatus.Confirmed;
        order.UpdatedAt = DateTime.UtcNow;

        // Save document record
        _db.Documents.Add(new Document
        {
            Type = DocumentType.PaymentSlip,
            CustomerId = customerId,
            ExtractedData = extractedJson,
            TotalAmount = payment.Amount,
            IsProcessed = true,
            ProcessingNotes =
                $"Payment confirmed for {order.OrderNumber}. " +
                $"Confidence: {payment.Confidence:P0}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Payment confirmed for order {OrderNumber} — LKR {Amount}",
            order.OrderNumber, payment.Amount);

        // Step 5 — Calculate delivery and send confirmation
        var deliveryMessage = "";
        if (!string.IsNullOrEmpty(order.DeliveryArea))
        {
            var delivery = await _deliveryService.CheckZoneAsync(
                order.DeliveryArea, order.TotalAmount);

            deliveryMessage = delivery.IsServiceable
                ? $"\n\n🚚 Delivery details:\n{delivery.Message}"
                : $"\n\n🚚 Delivery to {order.DeliveryArea} will be confirmed shortly.";
        }

        // Send confirmation to customer
        var confirmMsg =
            $"✅ Payment confirmed!\n\n" +
            $"Order: {order.OrderNumber}\n" +
            $"Amount: LKR {payment.Amount:N0}\n" +
            $"Reference: {payment.Reference ?? "N/A"}\n" +
            $"Bank: {payment.Bank ?? "N/A"}" +
            deliveryMessage +
            $"\n\nThank you! We'll start processing your order now. 🙏";

        await _whatsAppSendService.SendTextMessageAsync(fromPhone, confirmMsg);

        // Notify owner
        await AlertOwnerAsync(
            $"💰 Payment received!\n" +
            $"Order: {order.OrderNumber}\n" +
            $"Amount: LKR {payment.Amount:N0}\n" +
            $"Ref: {payment.Reference ?? "N/A"}\n" +
            $"Customer: {fromPhone}");

        return new PaymentVerificationResultDto
        {
            Success = true,
            OrderNumber = order.OrderNumber,
            AmountVerified = payment.Amount,
            Reference = payment.Reference,
            Message = "Payment confirmed successfully"
        };
    }

    private async Task<Order?> FindMatchingOrderAsync(
        int? customerId, decimal amount)
    {
        if (customerId == null) return null;

        // Try exact amount match first
        var exactMatch = await _db.Orders
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
            .Where(o => o.CustomerId == customerId &&
                        o.PaymentStatus == PaymentStatus.Unpaid &&
                        o.Status != OrderStatus.Cancelled &&
                        o.Status != OrderStatus.FraudPending)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();
    }

    private async Task AlertOwnerAsync(string message)
    {
        var ownerPhone = _config["OWNER_PHONE"];
        if (!string.IsNullOrEmpty(ownerPhone))
            await _whatsAppSendService.SendTextMessageAsync(ownerPhone, message);
    }
}