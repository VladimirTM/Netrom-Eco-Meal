using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Services;

// Periodic pass over time-based order transitions that nothing else would ever trigger: deletes
// PendingCheckouts nobody returned to from Stripe (abandoned before paying), cancels Pending
// orders that were never confirmed (and refunds them, since they were paid for at checkout time)
// so they stop locking stock via the pendingElsewhere reservation check in
// OrderService.PlaceOrderAsync, reminds customers shortly before a Confirmed order's pickup
// window closes, marks Confirmed orders whose pickup window fully closed as NoShow, restoring
// stock, and notifies staff about packages that closed completely unsold instead of expiring silently.
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
                var packageService = scope.ServiceProvider.GetRequiredService<IPackageService>();

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

                var donationNotices = await packageService.NotifyDonationCandidatesAsync();
                if (donationNotices > 0)
                    logger.LogInformation("Notified staff about {Count} unsold-at-cutoff package(s).", donationNotices);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to sweep order lifecycle transitions.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
