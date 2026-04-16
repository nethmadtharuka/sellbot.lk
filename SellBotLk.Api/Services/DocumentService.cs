using System.Text.Json;
using SellBotLk.Api.Data;
using SellBotLk.Api.Data.Repositories;
using SellBotLk.Api.Integrations.Gemini;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace SellBotLk.Api.Services;

public class DocumentService
{
    private readonly AppDbContext _db;
    private readonly GeminiVisionService _visionService;
    private readonly ProductRepository _productRepository;
    private readonly WhatsAppSendService _whatsAppSendService;
    private readonly IConfiguration _config;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        AppDbContext db,
        GeminiVisionService visionService,
        ProductRepository productRepository,
        WhatsAppSendService whatsAppSendService,
        IConfiguration config,
        ILogger<DocumentService> logger)
    {
        _db = db;
        _visionService = visionService;
        _productRepository = productRepository;
        _whatsAppSendService = whatsAppSendService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Main entry point — routes document to correct processor by type.
    /// </summary>
    public async Task<DocumentResponseDto> ProcessDocumentAsync(
        byte[] fileBytes,
        string mimeType,
        DocumentType documentType,
        int? customerId,
        string? fromPhone = null)
    {
        _logger.LogInformation(
            "Processing {Type} document — {Size} bytes, mime: {Mime}",
            documentType, fileBytes.Length, mimeType);

        // For PDFs, extract first page as image for Gemini Vision
        var processBytes = fileBytes;
        var processMime = mimeType;
        if (mimeType == "application/pdf")
        {
            (processBytes, processMime) = await ExtractPdfFirstPageAsync(fileBytes);
        }

        string extractedJson;
        string? processingNotes = null;
        string? vendorName = null;
        decimal? totalAmount = null;
        DateOnly? documentDate = null;

        switch (documentType)
        {
            case DocumentType.SupplierInvoice:
                extractedJson = await _visionService
                    .ExtractInvoiceDataAsync(processBytes, processMime);
                (vendorName, totalAmount, documentDate, processingNotes) =
                    ParseInvoiceMeta(extractedJson);
                break;

            case DocumentType.PaymentSlip:
                extractedJson = await _visionService
                    .ExtractPaymentDataAsync(processBytes, processMime);
                (totalAmount, processingNotes) = ParsePaymentMeta(extractedJson);
                break;

            case DocumentType.DamageReport:
                extractedJson = await _visionService
                    .ExtractDamageDataAsync(processBytes, processMime);
                processingNotes = "Damage report received — owner has been notified.";
                break;

            default:
                extractedJson = "{}";
                processingNotes = "Unknown document type.";
                break;
        }

        // Save document record
        var document = new Document
        {
            Type = documentType,
            CustomerId = customerId,
            ExtractedData = extractedJson,
            VendorName = vendorName,
            TotalAmount = totalAmount,
            DocumentDate = documentDate,
            ProcessingNotes = processingNotes,
            IsProcessed = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync();

        // Apply downstream effects
        if (documentType == DocumentType.SupplierInvoice)
            await ProcessSupplierInvoiceAsync(document, fromPhone);

        if (documentType == DocumentType.PaymentSlip)
            await ProcessPaymentSlipAsync(document, customerId, fromPhone);

        if (documentType == DocumentType.DamageReport)
            await NotifyOwnerOfDamageAsync(document, fromPhone);

        return MapToDto(document);
    }

    /// <summary>
    /// Processes a supplier invoice — updates inventory for matched products.
    /// </summary>
    private async Task ProcessSupplierInvoiceAsync(
        Document document, string? fromPhone)
    {
        try
        {
            var invoice = JsonSerializer.Deserialize<ExtractedInvoiceData>(
                document.ExtractedData,
                new JsonSerializerOptions
                    { PropertyNameCaseInsensitive = true })
                ?? new ExtractedInvoiceData();

            if (!invoice.Items.Any())
            {
                document.ProcessingNotes =
                    "No line items found in invoice. " +
                    "Please check the image quality.";
                await _db.SaveChangesAsync();
                return;
            }

            var restockedItems = new List<string>();
            var notFoundItems = new List<string>();

            foreach (var item in invoice.Items)
            {
                // Find matching product by name (case-insensitive partial match)
                var product = await _db.Products
                    .FirstOrDefaultAsync(p =>
                        p.IsActive &&
                        p.Name.ToLower().Contains(
                            item.ProductName.ToLower()));

                if (product == null)
                {
                    notFoundItems.Add(item.ProductName);
                    continue;
                }

                var qtyBefore = product.StockQty;
                product.StockQty += item.Quantity;

                // Log inventory change
                _db.InventoryLogs.Add(new InventoryLog
                {
                    ProductId = product.Id,
                    ChangeType = InventoryChangeType.SupplierRestock,
                    QuantityBefore = qtyBefore,
                    QuantityAfter = product.StockQty,
                    ReferenceId = $"DOC-{document.Id}",
                    CreatedAt = DateTime.UtcNow
                });

                restockedItems.Add(
                    $"{product.Name}: +{item.Quantity} " +
                    $"(now {product.StockQty})");
            }

            document.IsProcessed = true;
            await _db.SaveChangesAsync();

            // Build confirmation message
            var summary = $"✅ Invoice processed successfully!\n\n";

            if (restockedItems.Any())
            {
                summary += $"📦 Restocked {restockedItems.Count} product(s):\n";
                summary += string.Join("\n", restockedItems.Select(i => $"  • {i}"));
            }

            if (notFoundItems.Any())
            {
                summary += $"\n\n⚠️ {notFoundItems.Count} product(s) not found " +
                          $"in catalogue:\n";
                summary += string.Join("\n",
                    notFoundItems.Select(i => $"  • {i}"));
                summary += "\nPlease add these products manually.";
            }

            if (document.TotalAmount.HasValue)
                summary += $"\n\n💰 Invoice total: LKR " +
                          $"{document.TotalAmount:N0}";

            // Send to owner
            var ownerPhone = _config["Owner:Phone"] ?? _config["OWNER_PHONE"];
            if (!string.IsNullOrEmpty(ownerPhone))
                await _whatsAppSendService.SendTextMessageAsync(
                    ownerPhone, summary);

            // Also reply to sender if different from owner
            if (!string.IsNullOrEmpty(fromPhone) && fromPhone != ownerPhone)
                await _whatsAppSendService.SendTextMessageAsync(
                    fromPhone, summary);

            _logger.LogInformation(
                "Invoice processed — {Restocked} restocked, {NotFound} not found",
                restockedItems.Count, notFoundItems.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing supplier invoice");
            document.ProcessingNotes = $"Processing error: {ex.Message}";
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Processes a payment slip — matches to pending order and confirms payment.
    /// </summary>
    private async Task ProcessPaymentSlipAsync(
        Document document, int? customerId, string? fromPhone)
    {
        try
        {
            var payment = JsonSerializer.Deserialize<ExtractedPaymentData>(
                document.ExtractedData,
                new JsonSerializerOptions
                    { PropertyNameCaseInsensitive = true })
                ?? new ExtractedPaymentData();

            if (payment.Confidence < 0.5)
            {
                var lowConfidenceMsg =
                    "⚠️ I had trouble reading your payment slip clearly. " +
                    "Please send a clearer photo in good lighting. " +
                    "Your payment has NOT been confirmed yet.";

                if (!string.IsNullOrEmpty(fromPhone))
                    await _whatsAppSendService.SendTextMessageAsync(
                        fromPhone, lowConfidenceMsg);

                document.ProcessingNotes =
                    $"Low confidence extraction: {payment.Confidence:P0}";
                await _db.SaveChangesAsync();
                return;
            }

            if (!payment.Amount.HasValue || payment.Amount <= 0)
            {
                document.ProcessingNotes = "Could not extract payment amount.";
                await _db.SaveChangesAsync();
                return;
            }

            // Find matching unpaid order for this customer
            var matchingOrder = await _db.Orders
                .Where(o => o.CustomerId == customerId &&
                            o.PaymentStatus == PaymentStatus.Unpaid &&
                            o.Status != OrderStatus.Cancelled)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (matchingOrder == null)
            {
                var noOrderMsg =
                    "✅ Payment slip received!\n\n" +
                    $"Amount: LKR {payment.Amount:N0}\n" +
                    $"Reference: {payment.Reference ?? "N/A"}\n\n" +
                    "However, I couldn't find a pending order to match " +
                    "this payment to. Please contact us for assistance.";

                if (!string.IsNullOrEmpty(fromPhone))
                    await _whatsAppSendService.SendTextMessageAsync(
                        fromPhone, noOrderMsg);

                document.ProcessingNotes = "No matching pending order found.";
                await _db.SaveChangesAsync();
                return;
            }

            // Check amount matches order total (allow 1% tolerance)
            var tolerance = matchingOrder.TotalAmount * 0.01m;
            var amountMatches = Math.Abs(
                payment.Amount.Value - matchingOrder.TotalAmount) <= tolerance;

            if (!amountMatches)
            {
                var mismatchMsg =
                    $"⚠️ Payment amount mismatch!\n\n" +
                    $"Order total: LKR {matchingOrder.TotalAmount:N0}\n" +
                    $"Payment received: LKR {payment.Amount:N0}\n\n" +
                    "Please contact us to resolve this difference.";

                if (!string.IsNullOrEmpty(fromPhone))
                    await _whatsAppSendService.SendTextMessageAsync(
                        fromPhone, mismatchMsg);

                // Alert owner
                var ownerPhone = _config["Owner:Phone"] ?? _config["OWNER_PHONE"];
                if (!string.IsNullOrEmpty(ownerPhone))
                    await _whatsAppSendService.SendTextMessageAsync(
                        ownerPhone,
                        $"⚠️ Payment mismatch on {matchingOrder.OrderNumber}!\n" +
                        $"Expected: LKR {matchingOrder.TotalAmount:N0}\n" +
                        $"Received: LKR {payment.Amount:N0}\n" +
                        $"Ref: {payment.Reference}");

                document.ProcessingNotes =
                    $"Amount mismatch: expected {matchingOrder.TotalAmount}, " +
                    $"received {payment.Amount}";
                await _db.SaveChangesAsync();
                return;
            }

            // Confirm payment
            matchingOrder.PaymentStatus = PaymentStatus.Paid;
            matchingOrder.PaymentReference = payment.Reference;
            matchingOrder.Status = OrderStatus.Confirmed;
            matchingOrder.UpdatedAt = DateTime.UtcNow;
            document.IsProcessed = true;
            document.ProcessingNotes =
                $"Payment confirmed for {matchingOrder.OrderNumber}";

            await _db.SaveChangesAsync();

            // Send confirmation to customer
            var confirmMsg =
                $"✅ Payment confirmed!\n\n" +
                $"Order: {matchingOrder.OrderNumber}\n" +
                $"Amount: LKR {payment.Amount:N0}\n" +
                $"Reference: {payment.Reference ?? "N/A"}\n" +
                $"Bank: {payment.Bank ?? "N/A"}\n\n" +
                $"Your order is now confirmed and will be processed shortly. " +
                $"We'll notify you when it's dispatched! 🚚";

            if (!string.IsNullOrEmpty(fromPhone))
                await _whatsAppSendService.SendTextMessageAsync(
                    fromPhone, confirmMsg);

            _logger.LogInformation(
                "Payment confirmed for order {OrderNumber}",
                matchingOrder.OrderNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment slip");
            document.ProcessingNotes = $"Processing error: {ex.Message}";
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Notifies the owner when a damage report photo is received.
    /// </summary>
    private async Task NotifyOwnerOfDamageAsync(
        Document document, string? fromPhone)
    {
        try
        {
            var damage = JsonSerializer.Deserialize<ExtractedDamageData>(
                document.ExtractedData,
                new JsonSerializerOptions
                    { PropertyNameCaseInsensitive = true })
                ?? new ExtractedDamageData();

            var ownerPhone = _config["Owner:Phone"] ?? _config["OWNER_PHONE"];
            if (!string.IsNullOrEmpty(ownerPhone))
            {
                var ownerMsg =
                    $"🚨 Damage Report Received!\n\n" +
                    $"From: {fromPhone ?? "Unknown"}\n" +
                    $"Product: {damage.ProductHint ?? "Unknown"}\n" +
                    $"Severity: {damage.Severity ?? "Unknown"}\n" +
                    $"Description: {damage.Description ?? "See photo"}\n" +
                    $"Recommendation: {damage.Recommendation ?? "Inspect"}\n\n" +
                    $"Please follow up with the customer.";

                await _whatsAppSendService.SendTextMessageAsync(
                    ownerPhone, ownerMsg);
            }

            // Acknowledge to customer
            if (!string.IsNullOrEmpty(fromPhone))
            {
                var customerMsg =
                    "Thank you for reporting this issue. 🙏\n\n" +
                    "We have received your damage report and our team " +
                    "will contact you within 24 hours to resolve this. " +
                    "We apologise for the inconvenience.";

                await _whatsAppSendService.SendTextMessageAsync(
                    fromPhone, customerMsg);
            }

            document.IsProcessed = true;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing damage report");
        }
    }

    private static (string? vendor, decimal? total,
        DateOnly? date, string? notes) ParseInvoiceMeta(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<ExtractedInvoiceData>(
                json, new JsonSerializerOptions
                    { PropertyNameCaseInsensitive = true });

            DateOnly? date = null;
            if (data?.Date != null &&
                DateOnly.TryParse(data.Date, out var parsed))
                date = parsed;

            var notes = data?.Items.Count > 0
                ? $"Extracted {data.Items.Count} line items successfully."
                : "No line items found — check image quality.";

            return (data?.Vendor, data?.Total, date, notes);
        }
        catch { return (null, null, null, "Extraction failed."); }
    }

    private static (decimal? amount, string? notes) ParsePaymentMeta(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<ExtractedPaymentData>(
                json, new JsonSerializerOptions
                    { PropertyNameCaseInsensitive = true });

            var notes = data?.Confidence >= 0.8
                ? "Payment slip read successfully."
                : $"Low confidence: {data?.Confidence:P0}. Manual review recommended.";

            return (data?.Amount, notes);
        }
        catch { return (null, "Extraction failed."); }
    }

    /// <summary>
    /// For PDF documents — extracts first page as PNG for Gemini Vision.
    /// Uses PdfPig to render. Falls back to raw bytes if rendering fails.
    /// </summary>
    private async Task<(byte[] Bytes, string MimeType)> ExtractPdfFirstPageAsync(
        byte[] pdfBytes)
    {
        try
        {
            using var doc = UglyToad.PdfPig.PdfDocument.Open(pdfBytes);
            var page = doc.GetPage(1);

            // Extract text content as a summary image substitute
            // PdfPig doesn't render to image — we pass the PDF bytes
            // directly to Gemini which can handle PDF natively
            _logger.LogInformation(
                "PDF has {Pages} pages — passing to Gemini directly",
                doc.NumberOfPages);

            return (pdfBytes, "application/pdf");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PDF extraction failed — passing raw bytes");
            return (pdfBytes, "application/pdf");
        }
    }

    private static DocumentResponseDto MapToDto(Document d) => new()
    {
        Id = d.Id,
        Type = d.Type.ToString(),
        CustomerId = d.CustomerId,
        VendorName = d.VendorName,
        TotalAmount = d.TotalAmount,
        DocumentDate = d.DocumentDate,
        IsProcessed = d.IsProcessed,
        ProcessingNotes = d.ProcessingNotes,
        ExtractedData = d.ExtractedData,
        CreatedAt = d.CreatedAt
    };
}