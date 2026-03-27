using Microsoft.AspNetCore.Mvc;
using SellBotLk.Api.Models.Entities;
using SellBotLk.Api.Services;
using SellBotLk.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace SellBotLk.Api.Controllers;

[ApiController]
[Route("api/v1/documents")]
public class DocumentsController : ControllerBase
{
    private readonly DocumentService _documentService;
    private readonly AppDbContext _db;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        DocumentService documentService,
        AppDbContext db,
        ILogger<DocumentsController> logger)
    {
        _documentService = documentService;
        _db = db;
        _logger = logger;
    }

    /// <summary>Get all processed documents</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.Documents.AsNoTracking();

        if (!string.IsNullOrEmpty(type) &&
            Enum.TryParse<DocumentType>(type, out var docType))
            query = query.Where(d => d.Type == docType);

        var total = await query.CountAsync();
        var documents = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            success = true,
            data = documents,
            total,
            page,
            pageSize
        });
    }
    

    /// <summary>Get a single document by ID</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var document = await _db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);

        if (document == null)
            return NotFound(new { success = false,
                error = $"Document {id} not found" });

        return Ok(new { success = true, data = document });
    }

    /// <summary>
    /// Upload and process a document (invoice, payment slip, damage photo).
    /// Accepts image files and PDFs up to 10MB.
    /// </summary>
    [HttpPost("process")]
    public async Task<IActionResult> ProcessDocument(
        [FromForm] IFormFile file,
        [FromForm] string documentType,
        [FromForm] int? customerId = null)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { success = false,
                error = "No file provided" });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { success = false,
                error = "File too large. Maximum size is 10MB." });

        var mimeType = file.ContentType.ToLower();
        if (!MediaDownloadService.IsAcceptedType(mimeType))
            return BadRequest(new { success = false,
                error = "Invalid file type. Accepted: JPEG, PNG, WebP, PDF" });

        if (!Enum.TryParse<DocumentType>(documentType, out var docType))
            return BadRequest(new { success = false,
                error = $"Invalid document type: {documentType}. " +
                        "Valid: SupplierInvoice, PaymentSlip, " +
                        "DamageReport, CustomerBill" });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var fileBytes = ms.ToArray();

        var result = await _documentService.ProcessDocumentAsync(
            fileBytes, mimeType, docType, customerId);

        return Ok(new { success = true, data = result });
    }
}