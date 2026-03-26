using System.ComponentModel.DataAnnotations;

namespace SellBotLk.Api.Models.DTOs;

public class ProductResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal BulkDiscountPercent { get; set; }
    public int BulkMinQty { get; set; }
    public int StockQty { get; set; }
    public string? Color { get; set; }
    public string? Material { get; set; }
    public string? Style { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public bool IsLowStock { get; set; }
}

public class CreateProductDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    [Range(1, 999999999)]
    public decimal Price { get; set; }

    [Required]
    [Range(1, 999999999)]
    public decimal MinPrice { get; set; }

    public decimal BulkDiscountPercent { get; set; } = 0;
    public int BulkMinQty { get; set; } = 10;

    [Range(0, int.MaxValue)]
    public int StockQty { get; set; } = 0;

    public int LowStockThreshold { get; set; } = 5;
    public string? Color { get; set; }
    public string? Material { get; set; }
    public string? Style { get; set; }
    public string? ImageUrl { get; set; }
}

public class UpdateProductDto
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public string? Description { get; set; }

    [Range(1, 999999999)]
    public decimal? Price { get; set; }

    [Range(1, 999999999)]
    public decimal? MinPrice { get; set; }

    public decimal? BulkDiscountPercent { get; set; }
    public int? BulkMinQty { get; set; }
    public int? StockQty { get; set; }
    public int? LowStockThreshold { get; set; }
    public string? Color { get; set; }
    public string? Material { get; set; }
    public string? Style { get; set; }
    public string? ImageUrl { get; set; }
    public bool? IsActive { get; set; }
}

public class ProductSearchRequestDto
{
    [Required]
    public string Query { get; set; } = null!;
    public string? Category { get; set; }
    public decimal? MaxPrice { get; set; }
    public int TopResults { get; set; } = 3;
}