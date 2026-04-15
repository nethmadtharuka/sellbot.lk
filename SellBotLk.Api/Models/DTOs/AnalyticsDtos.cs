namespace SellBotLk.Api.Models.DTOs;

public class AnalyticsSummaryDto
{
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public int PaidOrders { get; set; }
    public int UnpaidOrders { get; set; }
    public List<StatusCountDto> OrdersByStatus { get; set; } = new();
    public List<RevenueByDayDto> RevenueByDay { get; set; } = new();
    public List<TopProductDto> TopProducts { get; set; } = new();
    public List<RecentOrderDto> RecentOrders { get; set; } = new();
}

public class StatusCountDto
{
    public string Status { get; set; } = "";
    public int Count { get; set; }
}

public class RevenueByDayDto
{
    public string Date { get; set; } = "";
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
}

public class TopProductDto
{
    public string ProductName { get; set; } = "";
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
}

public class RecentOrderDto
{
    public string OrderNumber { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string Status { get; set; } = "";
    public string PaymentStatus { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}
