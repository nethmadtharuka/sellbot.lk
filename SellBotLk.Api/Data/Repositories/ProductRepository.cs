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

    /// <summary>
    /// Atomically takes stock with a single conditional UPDATE, so concurrent orders
    /// cannot oversell. Call inside a transaction: the row stays locked until commit,
    /// which keeps the audit log's before/after figures exact.
    /// Returns the stock left, or null if there was not enough (or the product is inactive).
    /// </summary>
    public async Task<int?> TryDeductStockAsync(int productId, int quantity)
    {
        var rows = await _db.Products
            .Where(p => p.Id == productId && p.IsActive && p.StockQty >= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQty, p => p.StockQty - quantity));

        if (rows == 0) return null;

        return await ReadStockAsync(productId);
    }

    /// <summary>
    /// Atomically adds stock and queues an audit row for the caller's SaveChanges.
    /// Call inside a transaction. Returns the new stock level.
    /// </summary>
    public async Task<int> RestockAsync(
        int productId, int quantity, InventoryChangeType changeType, string reference)
    {
        await _db.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQty, p => p.StockQty + quantity));

        var after = await ReadStockAsync(productId);
        LogStockChange(productId, changeType, after - quantity, after, reference);
        return after;
    }

    /// <summary>Drops audit rows from a failed transaction so a later save can't insert them.</summary>
    public void DiscardPendingLogs()
    {
        foreach (var entry in _db.ChangeTracker.Entries<InventoryLog>()
                     .Where(e => e.State == EntityState.Added))
            entry.State = EntityState.Detached;
    }

    private Task<int> ReadStockAsync(int productId) =>
        _db.Products.AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => p.StockQty)
            .FirstAsync();

    /// <summary>Queues an audit row; the caller's SaveChanges writes it.</summary>
    public void LogStockChange(
        int productId, InventoryChangeType changeType, int before, int after, string reference)
    {
        _db.InventoryLogs.Add(new InventoryLog
        {
            ProductId = productId,
            ChangeType = changeType,
            QuantityBefore = before,
            QuantityAfter = after,
            ReferenceId = reference,
            CreatedAt = DateTime.UtcNow
        });
    }
}
