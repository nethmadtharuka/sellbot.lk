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
    private readonly PaymentMatchingService _paymentMatchingService;
    private readonly IConfiguration _config;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        AppDbContext db,
        GeminiVisionService visionService,
        ProductRepository productRepository,
        WhatsAppSendService whatsAppSendService,
        PaymentMatchingService paymentMatchingService,
        IConfiguration config,
        ILogger<DocumentService> logger)
    {
        _db = db;
        _visionService = visionService;
        _productRepository = productRepository;
        _whatsAppSendService = whatsAppSendService;
        _paymentMatchingService = paymentMatchingService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// True only when the phone matches the configured owner number.
    /// With no owner configured nobody matches, so this fails closed.
    /// </summary>
    public bool IsOwnerPhone(string? phone)
    {
        var owner = DigitsOnly(_config["Owner:Phone"] ?? _config["OWNER_PHONE"]);
        return owner.Length > 0 && owner == DigitsOnly(phone);
    }

    private static string DigitsOnly(string? value) =>
        new((value ?? "").Where(char.IsDigit).ToArray());

    /// <summary>
    /// Main entry point — routes document to correct processor by type.
    /// A null fromPhone means the upload came through the JWT-protected admin API.
    /// Any phone number must belong to the owner before an invoice can touch stock.
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

        if (documentType == DocumentType.SupplierInvoice &&
            fromPhone != null && !IsOwnerPhone(fromPhone))
        {
            _logger.LogWarning("Rejected supplier invoice from a non-owner phone number");
            throw new UnauthorizedAccessException(
                "Only the business owner can submit supplier invoices.");
        }

        // Slips have one pipeline, shared with WhatsApp, which never marks an order Paid.
        if (documentType == DocumentType.PaymentSlip)
        {
            var result = await _paymentMatchingService.ProcessPaymentSlipAsync(
                fileBytes, mimeType, customerId, fromPhone);

            return MapToDto((await _db.Documents.FindAsync(result.DocumentId))!);
        }

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

            await using (var transaction = await _db.Database.BeginTransactionAsync())
            {
                foreach (var item in invoice.Items)
                {
                    // AI-read values: a blank name would match every product, and a
                    // zero or negative quantity must never change stock.
                    if (string.IsNullOrWhiteSpace(item.ProductName) || item.Quantity < 1)
                    {
                        notFoundItems.Add(
                            $"{item.ProductName ?? "(no name)"} (invalid line, qty {item.Quantity})");
                        continue;
                    }

                    // Find matching product by name (case-insensitive partial match)
                    var product = await _db.Products
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p =>
                            p.IsActive &&
                            p.Name.ToLower().Contains(
                                item.ProductName.ToLower()));

                    if (product == null)
                    {
                        notFoundItems.Add(item.ProductName);
                        continue;
                    }

                    var newStock = await _productRepository.RestockAsync(
                        product.Id, item.Quantity,
                        InventoryChangeType.SupplierRestock, $"DOC-{document.Id}");

                    restockedItems.Add(
                        $"{product.Name}: +{item.Quantity} " +
                        $"(now {newStock})");
                }

                document.IsProcessed = true;
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            }

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

            _logger.LogInformation(
                "Invoice processed — {Restocked} restocked, {NotFound} not found",
                restockedItems.Count, notFoundItems.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing supplier invoice");
            _productRepository.DiscardPendingLogs();
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