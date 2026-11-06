using Microsoft.EntityFrameworkCore;
using SellBotLk.Api.Data;

namespace SellBotLk.Api.Services;

/// <summary>
/// Periodic housekeeping: releases stock held by expired unpaid orders and
/// removes old webhook dedupe rows. Safe to run on several instances, because
/// each cancellation is a conditional update that only one of them can win.
/// </summary>
public class MaintenanceService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    // Meta retries a failed delivery for up to 7 days; keep IDs well beyond that.
    private static readonly TimeSpan DedupeRetention = TimeSpan.FromDays(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceService> _logger;

    public MaintenanceService(
        IServiceScopeFactory scopeFactory,
        ILogger<MaintenanceService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                await scope.ServiceProvider.GetRequiredService<OrderService>()
                    .ExpireUnpaidOrdersAsync();

                var cutoff = DateTime.UtcNow - DedupeRetention;
                await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                    .ProcessedWebhookMessages
                    .Where(m => m.ProcessedAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Maintenance run failed — will retry on the next tick");
            }
        }
    }
}
