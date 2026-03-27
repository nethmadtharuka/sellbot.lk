using SellBotLk.Api.Data.Repositories;
using SellBotLk.Api.Integrations.Gemini;
using SellBotLk.Api.Models.DTOs;

namespace SellBotLk.Api.Services;

public class VisualSearchService
{
    private readonly GeminiService _geminiService;
    private readonly ProductRepository _productRepository;
    private readonly ILogger<VisualSearchService> _logger;

    public VisualSearchService(
        GeminiService geminiService,
        ProductRepository productRepository,
        ILogger<VisualSearchService> logger)
    {
        _geminiService = geminiService;
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task<VisualSearchResultDto> SearchByImageAsync(
        byte[] imageBytes, string mimeType)
    {
        // Step 1 — Gemini analyzes the image
        _logger.LogInformation("Starting visual search with Gemini Vision");
        var attributes = await _geminiService.AnalyzeImageAsync(imageBytes, mimeType);

        _logger.LogInformation(
            "Image analyzed — Type:{Type} Color:{Color} Material:{Material} Style:{Style}",
            attributes.Type, attributes.Color, attributes.Material, attributes.Style);

        // Step 2 — Score all products against extracted attributes
        var allProducts = await _productRepository.GetAllActiveAsync();

        var scored = allProducts
            .Select(p => new
            {
                Product = p,
                Score = CalculateSimilarityScore(p, attributes)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(3)
            .ToList();

        // Step 3 — Build result
        var matches = scored.Select(x => new ProductMatchDto
        {
            Id = x.Product.Id,
            Name = x.Product.Name,
            Category = x.Product.Category,
            Price = x.Product.Price,
            Color = x.Product.Color,
            Material = x.Product.Material,
            Style = x.Product.Style,
            ImageUrl = x.Product.ImageUrl,
            StockQty = x.Product.StockQty,
            SimilarityScore = x.Score
        }).ToList();

        return new VisualSearchResultDto
        {
            DetectedAttributes = attributes,
            Matches = matches,
            HasMatches = matches.Any()
        };
    }

    private static int CalculateSimilarityScore(
        Models.Entities.Product product, VisualSearchAttributes attributes)
    {
        int score = 0;

        // Category/type match — highest weight
        if (attributes.Type != null &&
            product.Category.Contains(attributes.Type,
                StringComparison.OrdinalIgnoreCase))
            score += 40;

        // Color match
        if (attributes.Color != null && product.Color != null &&
            product.Color.Contains(attributes.Color,
                StringComparison.OrdinalIgnoreCase))
            score += 25;

        // Material match
        if (attributes.Material != null && product.Material != null &&
            product.Material.Contains(attributes.Material,
                StringComparison.OrdinalIgnoreCase))
            score += 20;

        // Style match
        if (attributes.Style != null && product.Style != null &&
            product.Style.Contains(attributes.Style,
                StringComparison.OrdinalIgnoreCase))
            score += 15;

        return score;
    }

    public string FormatVisualSearchResultForWhatsApp(VisualSearchResultDto result)
    {
        if (!result.HasMatches)
        {
            return "🔍 I analyzed your image but couldn't find similar products " +
                   "in our catalogue.\n\n" +
                   "Type *browse* to see all available items, or describe " +
                   "what you're looking for!";
        }

        var attrs = result.DetectedAttributes;
        var lines = new List<string>
        {
            $"🔍 I can see a *{attrs.Color} {attrs.Type}* " +
            $"({attrs.Material}, {attrs.Style} style)\n",
            "Here are our closest matches:\n"
        };

        foreach (var p in result.Matches)
        {
            var stockStatus = p.StockQty == 0 ? "❌ Out of stock" :
                              p.StockQty <= 5 ? $"⚠️ Only {p.StockQty} left" :
                              "✅ In stock";

            lines.Add($"*{p.Name}*\n" +
                     $"   💰 LKR {p.Price:N0}\n" +
                     $"   🎨 {p.Color} | {p.Material}\n" +
                     $"   {stockStatus}");
        }

        lines.Add("\nReply with a product name to order or ask for more details!");
        return string.Join("\n", lines);
    }
}