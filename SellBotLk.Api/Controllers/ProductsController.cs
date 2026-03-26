using Microsoft.AspNetCore.Mvc;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Services;

namespace SellBotLk.Api.Controllers;

[ApiController]
[Route("api/v1/products")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _productService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        ProductService productService,
        ILogger<ProductsController> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    /// <summary>Get all active products</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _productService.GetAllAsync();
        return Ok(new { success = true, data = products, count = products.Count });
    }

    /// <summary>Get a single product by ID</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null)
            return NotFound(new { success = false,
                error = $"Product {id} not found" });

        return Ok(new { success = true, data = product });
    }

    /// <summary>Get all products below low stock threshold</summary>
    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock()
    {
        var products = await _productService.GetLowStockAsync();
        return Ok(new { success = true, data = products, count = products.Count });
    }

    /// <summary>Create a new product</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, errors = ModelState });

        var product = await _productService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById),
            new { id = product.Id },
            new { success = true, data = product });
    }

    /// <summary>Update an existing product</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductDto dto)
    {
        var product = await _productService.UpdateAsync(id, dto);
        if (product == null)
            return NotFound(new { success = false,
                error = $"Product {id} not found" });

        return Ok(new { success = true, data = product });
    }

    /// <summary>Soft delete a product</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _productService.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { success = false,
                error = $"Product {id} not found" });

        return Ok(new { success = true,
            message = $"Product {id} deactivated" });
    }

    /// <summary>Smart AI-powered product search</summary>
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] ProductSearchRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, errors = ModelState });

        var products = await _productService.SmartSearchAsync(
            dto.Query, dto.Category, dto.MaxPrice);

        var formatted = _productService.FormatProductsForWhatsApp(products);

        return Ok(new { success = true, data = products,
            count = products.Count, whatsAppMessage = formatted });
    }
}