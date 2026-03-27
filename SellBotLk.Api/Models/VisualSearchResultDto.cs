namespace SellBotLk.Api.Models.DTOs;

public class VisualSearchResultDto
{
    public VisualSearchAttributes DetectedAttributes { get; set; } = new();
    public List<ProductMatchDto> Matches { get; set; } = new();
    public bool HasMatches { get; set; }
}

public class ProductMatchDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public string? Color { get; set; }
    public string? Material { get; set; }
    public string? Style { get; set; }
    public string? ImageUrl { get; set; }
    public int StockQty { get; set; }
    public int SimilarityScore { get; set; }
}