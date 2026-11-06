using Microsoft.EntityFrameworkCore;
using SellBotLk.Api.Models.Entities;

namespace SellBotLk.Api.Data.Repositories;

public class OrderRepository
{
    private readonly AppDbContext _db;

    public OrderRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Order?> GetByIdAsync(int id)
    {
        return await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Order?> GetByOrderNumberAsync(string orderNumber)
    {
        return await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
    }

    public async Task<List<Order>> GetAllAsync(
        OrderStatus? status = null,
        int? customerId = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var query = _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        if (customerId.HasValue)
            query = query.Where(o => o.CustomerId == customerId.Value);

        if (from.HasValue)
            query = query.Where(o => o.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(o => o.CreatedAt <= to.Value);

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Order>> GetByCustomerIdAsync(int customerId)
    {
        return await _db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<Order> CreateAsync(Order order)
    {
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return order;
    }

    /// <summary>
    /// Moves an order from one status to another only if it is still in the status
    /// the caller read. Stops a status change from reviving an order that was
    /// cancelled (and restocked) in the meantime. Returns false if it moved first.
    /// </summary>
    public async Task<bool> TryUpdateStatusAsync(
        int id, OrderStatus from, OrderStatus to, string? driverNote = null)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.Orders
            .Where(o => o.Id == id && o.Status == from)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, to)
                .SetProperty(o => o.UpdatedAt, now)
                .SetProperty(o => o.DriverNote, o => driverNote ?? o.DriverNote));

        return rows == 1;
    }

    public async Task<List<Order>> GetRecentOrdersByPhoneAsync(
        string phone, int minutes = 10)
    {
        var since = DateTime.UtcNow.AddMinutes(-minutes);
        return await _db.Orders
            .Include(o => o.Customer)
            .Where(o => o.Customer.PhoneNumber == phone
                     && o.CreatedAt >= since)
            .ToListAsync();
    }
}