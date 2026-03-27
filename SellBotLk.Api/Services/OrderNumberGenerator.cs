using SellBotLk.Api.Data.Repositories;

namespace SellBotLk.Api.Services;

public class OrderNumberGenerator
{
    private readonly OrderRepository _orderRepository;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public OrderNumberGenerator(OrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    /// <summary>
    /// Generates a unique order number in format ORD-2026-001.
    /// Thread-safe — uses semaphore to prevent duplicate numbers
    /// under concurrent requests.
    /// </summary>
    public async Task<string> GenerateAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var year = DateTime.UtcNow.Year;
            var count = await _orderRepository.GetOrderCountThisYearAsync();
            var sequence = count + 1;
            return $"ORD-{year}-{sequence:D3}";
        }
        finally
        {
            _lock.Release();
        }
    }
}