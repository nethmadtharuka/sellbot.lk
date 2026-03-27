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

    public async Task<Order?> UpdateStatusAsync(int id, OrderStatus status)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return null;

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return order;
    }

    public async Task<int> GetOrderCountThisYearAsync()
    {
        var startOfYear = new DateTime(DateTime.UtcNow.Year, 1, 1);
        return await _db.Orders
            .CountAsync(o => o.CreatedAt >= startOfYear);
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