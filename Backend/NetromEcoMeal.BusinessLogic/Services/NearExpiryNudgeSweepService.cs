using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Services;

// Periodic pass over packages closing soon with stock still unclaimed — same shape as
// OrderLifecycleSweepService, just a different time-based trigger. See NearExpiryNudgeService for
// the audience/AI-copy logic.
public class NearExpiryNudgeSweepService(IServiceScopeFactory scopeFactory, ILogger<NearExpiryNudgeSweepService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PackageNudgeSettings.SweepInterval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var nudgeService = scope.ServiceProvider.GetRequiredService<INearExpiryNudgeService>();

                var sent = await nudgeService.SweepAsync();
                if (sent > 0)
                    logger.LogInformation("Sent {Count} near-expiry nudge(s).", sent);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to sweep near-expiry package nudges.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
