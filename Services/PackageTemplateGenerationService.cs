using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

// Periodic pass that spins off today's Package instance from every active "repeat daily" template,
// so a manager who ticked "Repeat this every day" never has to hand-recreate the same package.
public class PackageTemplateGenerationService(IServiceScopeFactory scopeFactory, ILogger<PackageTemplateGenerationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PackageTemplateGeneration.SweepInterval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var templateService = scope.ServiceProvider.GetRequiredService<IPackageTemplateService>();

                var generated = await templateService.GenerateDueInstancesAsync();
                if (generated > 0)
                    logger.LogInformation("Generated {Count} package instance(s) from recurring templates.", generated);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate recurring package instances.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
