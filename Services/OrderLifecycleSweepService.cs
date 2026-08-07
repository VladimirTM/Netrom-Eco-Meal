using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

// Periodic pass over time-based order transitions that nothing else would ever trigger:
//   - deletes PendingCheckouts nobody returned to from Stripe (abandoned before paying);
//   - cancels Pending orders that were never confirmed, so they stop locking stock via the
//     pendingElsewhere reservation check in OrderService.PlaceOrderAsync (and refunds them, since
//     they were paid for at checkout time);
//   - reminds customers shortly before a Confirmed order's pickup window closes;
//   - marks Confirmed orders whose pickup window fully closed as NoShow, restoring stock.
public class OrderLifecycleSweepService(IServiceScopeFactory scopeFactory, ILogger<OrderLifecycleSweepService> logger) : BackgroundService
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
                var checkoutService = scope.ServiceProvider.GetRequiredService<ICheckoutService>();

                var expiredCheckouts = await checkoutService.ExpireStalePendingCheckoutsAsync();
                if (expiredCheckouts > 0)
                    logger.LogInformation("Expired {Count} stale pending checkout(s).", expiredCheckouts);

                var expired = await orderService.ExpireStalePendingOrdersAsync();
                if (expired > 0)
                    logger.LogInformation("Expired {Count} stale pending order(s).", expired);

                var reminded = await orderService.SendPickupRemindersAsync();
                if (reminded > 0)
                    logger.LogInformation("Sent {Count} pickup reminder(s).", reminded);

                var noShows = await orderService.ExpireNoShowOrdersAsync();
                if (noShows > 0)
                    logger.LogInformation("Marked {Count} order(s) as no-show.", noShows);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to sweep order lifecycle transitions.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
