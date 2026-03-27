using SellBotLk.Api.Data.Repositories;
using SellBotLk.Api.Integrations.Gemini;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Models.Entities;
using System.Text.Json;

namespace SellBotLk.Api.Services;

public class ProductService
{
    private readonly ProductRepository _productRepository;
    private readonly GeminiService _geminiService;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        ProductRepository productRepository,
        GeminiService geminiService,
        ILogger<ProductService> logger)
    {
        _productRepository = productRepository;
        _geminiService = geminiService;
        _logger = logger;
    }

    public async Task<List<ProductResponseDto>> GetAllAsync()
    {
        var products = await _productRepository.GetAllActiveAsync();
        return products.Select(MapToDto).ToList();
    }

    public async Task<ProductResponseDto?> GetByIdAsync(int id)
    {
        var product = await _productRepository.GetByIdAsync(id);
        return product == null ? null : MapToDto(product);
    }

    public async Task<List<ProductResponseDto>> GetLowStockAsync()
    {
        var products = await _productRepository.GetLowStockAsync();
        return products.Select(MapToDto).ToList();
    }

    public async Task<ProductResponseDto> CreateAsync(CreateProductDto dto)
    {
        var product = new Product
        {
            Name = dto.Name,
            Category = dto.Category,
            Description = dto.Description,
            Price = dto.Price,
            MinPrice = dto.MinPrice,
            BulkDiscountPercent = dto.BulkDiscountPercent,
            BulkMinQty = dto.BulkMinQty,
            StockQty = dto.StockQty,
            LowStockThreshold = dto.LowStockThreshold,
            Color = dto.Color,
            Material = dto.Material,
            Style = dto.Style,
            ImageUrl = dto.ImageUrl,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _productRepository.CreateAsync(product);
        return MapToDto(created);
    }

    public async Task<ProductResponseDto?> UpdateAsync(int id, UpdateProductDto dto)
    {
        var updated = await _productRepository.UpdateAsync(id, product =>
        {
            if (dto.Name != null) product.Name = dto.Name;
            if (dto.Category != null) product.Category = dto.Category;
            if (dto.Description != null) product.Description = dto.Description;
            if (dto.Price.HasValue) product.Price = dto.Price.Value;
            if (dto.MinPrice.HasValue) product.MinPrice = dto.MinPrice.Value;
            if (dto.BulkDiscountPercent.HasValue)
                product.BulkDiscountPercent = dto.BulkDiscountPercent.Value;
            if (dto.BulkMinQty.HasValue) product.BulkMinQty = dto.BulkMinQty.Value;
            if (dto.StockQty.HasValue) product.StockQty = dto.StockQty.Value;
            if (dto.LowStockThreshold.HasValue)
                product.LowStockThreshold = dto.LowStockThreshold.Value;
            if (dto.Color != null) product.Color = dto.Color;
            if (dto.Material != null) product.Material = dto.Material;
            if (dto.Style != null) product.Style = dto.Style;
            if (dto.ImageUrl != null) product.ImageUrl = dto.ImageUrl;
            if (dto.IsActive.HasValue) product.IsActive = dto.IsActive.Value;
        });

        return updated == null ? null : MapToDto(updated);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _productRepository.SoftDeleteAsync(id);
    }


    /// <summary>
    /// Smart search — uses Gemini to match a vague customer description
    /// like "red comfy chair" to the actual product catalogue.
    /// </summary>
    public async Task<List<ProductResponseDto>> SmartSearchAsync(
        string query, string? category = null, decimal? maxPrice = null)
    {
        var allProducts = await _productRepository.GetAllActiveAsync();

        // Apply basic filters first to reduce Gemini prompt size
        var filtered = allProducts.Where(p =>
            (category == null || p.Category == category) &&
            (maxPrice == null || p.Price <= maxPrice))
            .ToList();

        if (!filtered.Any())
            return new List<ProductResponseDto>();

        // Build a compact catalogue summary for Gemini
        var catalogue = filtered.Select(p =>
            $"ID:{p.Id} | {p.Name} | {p.Category} | " +
            $"LKR {p.Price} | Color:{p.Color} | " +
            $"Material:{p.Material} | Style:{p.Style} | " +
            $"Stock:{p.StockQty}").ToList();

        var catalogueText = string.Join("\n", catalogue);

        var prompt = $"""
            A customer is searching for: "{query}"
            
            Available products:
            {catalogueText}
            
            Return ONLY a JSON array of the top 3 most relevant product IDs.
            Example: [12, 5, 8]
            If no products match, return an empty array: []
            Do not include any explanation — just the JSON array.
            """;

        var response = await _geminiService.GenerateReplyAsync(prompt, "en");

        try
        {
            var cleaned = response.Replace("```json", "")
                .Replace("```", "").Trim();
            var start = cleaned.IndexOf('[');
            var end = cleaned.LastIndexOf(']');

            if (start >= 0 && end > start)
                cleaned = cleaned[start..(end + 1)];

            var ids = JsonSerializer.Deserialize<List<int>>(cleaned)
                ?? new List<int>();

            return ids
                .Select(id => filtered.FirstOrDefault(p => p.Id == id))
                .Where(p => p != null)
                .Select(p => MapToDto(p!))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Gemini search response");

            // Fallback — simple name/category text match
            return filtered
                .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            p.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .Select(MapToDto)
                .ToList();
        }
    }

    /// <summary>
    /// Formats products as a WhatsApp-friendly message string.
    /// </summary>
    public string FormatProductsForWhatsApp(List<ProductResponseDto> products)
    {
        if (!products.Any())
            return "Sorry, I couldn't find any matching products. " +
                   "Type 'browse' to see all available items.";

        var lines = new List<string>
        {
            $"Found {products.Count} matching product(s):\n"
        };

        foreach (var p in products)
        {
            var stockStatus = p.StockQty == 0 ? "❌ Out of stock" :
                              p.IsLowStock ? $"⚠️ Only {p.StockQty} left" :
                              $"✅ In stock ({p.StockQty} available)";

            var bulkInfo = p.BulkDiscountPercent > 0
                ? $"\n   🏷️ {p.BulkDiscountPercent}% off for {p.BulkMinQty}+ units"
                : "";

            lines.Add($"*{p.Name}*\n" +
                     $"   💰 LKR {p.Price:N0}\n" +
                     $"   {stockStatus}" +
                     bulkInfo);
        }

        lines.Add("\nReply with the product name to order, " +
                 "or ask for more details!");

        return string.Join("\n", lines);
    }

    private static ProductResponseDto MapToDto(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Category = p.Category,
        Description = p.Description,
        Price = p.Price,
        BulkDiscountPercent = p.BulkDiscountPercent,
        BulkMinQty = p.BulkMinQty,
        StockQty = p.StockQty,
        Color = p.Color,
        Material = p.Material,
        Style = p.Style,
        ImageUrl = p.ImageUrl,
        IsActive = p.IsActive,
        IsLowStock = p.StockQty <= p.LowStockThreshold
    };
}