using Microsoft.EntityFrameworkCore;
using SellBotLk.Api.Models.Entities;

namespace SellBotLk.Api.Data.Repositories;

public class ProductRepository
{
    private readonly AppDbContext _db;

    public ProductRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Product>> GetAllActiveAsync()
    {
        return await _db.Products
            .Where(p => p.IsActive)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Product>> GetByCategoryAsync(string category)
    {
        return await _db.Products
            .Where(p => p.IsActive && p.Category == category)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Product>> GetLowStockAsync()
    {
        return await _db.Products
            .Where(p => p.IsActive && p.StockQty <= p.LowStockThreshold)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Product> CreateAsync(Product product)
    {
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<Product?> UpdateAsync(int id, Action<Product> updateAction)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return null;

        updateAction(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<bool> SoftDeleteAsync(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return false;

        product.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }
    public async Task DeductStockAsync(int productId, int quantity)
{
    var product = await _db.Products.FindAsync(productId);
    if (product == null) return;

    product.StockQty -= quantity;
    await _db.SaveChangesAsync();
}

public async Task RestockAsync(int productId, int quantity)
{
    var product = await _db.Products.FindAsync(productId);
    if (product == null) return;

    product.StockQty += quantity;
    await _db.SaveChangesAsync();
}
}