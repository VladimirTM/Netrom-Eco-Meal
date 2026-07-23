using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

// Periodically cancels Pending orders that were never confirmed, so they stop locking stock via
// the pendingElsewhere reservation check in OrderService.PlaceOrderAsync.
public class PendingOrderExpiryService(IServiceScopeFactory scopeFactory, ILogger<PendingOrderExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(OrderExpiry.SweepInterval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                var expired = await orderService.ExpireStalePendingOrdersAsync();
                if (expired > 0)
                    logger.LogInformation("Expired {Count} stale pending order(s).", expired);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to sweep stale pending orders.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
