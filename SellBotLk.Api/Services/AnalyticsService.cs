using Microsoft.EntityFrameworkCore;
using SellBotLk.Api.Data;
using SellBotLk.Api.Models.DTOs;
using SellBotLk.Api.Models.Entities;

namespace SellBotLk.Api.Services;

public class AnalyticsService
{
    private readonly AppDbContext _db;

    public AnalyticsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AnalyticsSummaryDto> GetSummaryAsync(DateTime from, DateTime to)
    {
        var orders = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Where(o => o.CreatedAt >= from && o.CreatedAt <= to)
            .ToListAsync();

        var totalOrders = orders.Count;
        var totalRevenue = orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .Sum(o => o.TotalAmount);
        var paidOrders = orders.Count(o => o.PaymentStatus == PaymentStatus.Paid);
        var unpaidOrders = orders.Count(o => o.PaymentStatus == PaymentStatus.Unpaid);

        var ordersByStatus = orders
            .GroupBy(o => o.Status.ToString())
            .Select(g => new StatusCountDto { Status = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var revenueByDay = orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new RevenueByDayDto
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                Revenue = g.Sum(o => o.TotalAmount),
                Orders = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToList();

        // Fill missing days with zeroes for a continuous chart line
        var allDays = Enumerable.Range(0, (to.Date - from.Date).Days + 1)
            .Select(d => from.Date.AddDays(d).ToString("yyyy-MM-dd"))
            .ToHashSet();
        var existingDays = revenueByDay.Select(r => r.Date).ToHashSet();
        foreach (var day in allDays.Except(existingDays))
            revenueByDay.Add(new RevenueByDayDto { Date = day, Revenue = 0, Orders = 0 });
        revenueByDay = revenueByDay.OrderBy(x => x.Date).ToList();

        var topProducts = orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SelectMany(o => o.Items)
            .GroupBy(i => i.Product?.Name ?? "Unknown")
            .Select(g => new TopProductDto
            {
                ProductName = g.Key,
                UnitsSold = g.Sum(i => i.Quantity),
                Revenue = g.Sum(i => i.LineTotal)
            })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToList();

        var recentOrders = orders
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .Select(o => new RecentOrderDto
            {
                OrderNumber = o.OrderNumber,
                CustomerName = o.Customer?.Name ?? "",
                Status = o.Status.ToString(),
                PaymentStatus = o.PaymentStatus.ToString(),
                TotalAmount = o.TotalAmount,
                CreatedAt = o.CreatedAt
            })
            .ToList();

        return new AnalyticsSummaryDto
        {
            TotalOrders = totalOrders,
            TotalRevenue = totalRevenue,
            PaidOrders = paidOrders,
            UnpaidOrders = unpaidOrders,
            OrdersByStatus = ordersByStatus,
            RevenueByDay = revenueByDay,
            TopProducts = topProducts,
            RecentOrders = recentOrders
        };
    }
}
