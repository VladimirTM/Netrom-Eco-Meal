using Microsoft.AspNetCore.Identity;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Models;
using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Services;

public class ImpactService(
    IOrderRepository orderRepository,
    IPackageRepository packageRepository,
    IBusinessRepository businessRepository,
    IStreakService streakService,
    UserManager<ApplicationUser> userManager,
    ICurrentUser currentUser) : IImpactService
{
    public async Task<List<LeaderboardEntry>> GetMonthlyLeaderboardAsync(int take = 20)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEndExclusive = monthStart.AddMonths(1);
        var entries = await orderRepository.GetTopRescuersAsync(monthStart, monthEndExclusive, take);

        // The board is capped at `take` rows, so a per-row streak lookup here stays cheap — no
        // reason to push this into a bulk repository query for a leaderboard this small.
        var withStreaks = new List<LeaderboardEntry>(entries.Count);
        foreach (var entry in entries)
            withStreaks.Add(entry with { StreakWeeks = await streakService.GetStreakWeeksAsync(entry.UserId) });

        return withStreaks;
    }

    public async Task<bool> GetMyOptInStatusAsync()
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            return false;

        var user = await userManager.FindByIdAsync(userId);
        return user?.ShowOnLeaderboard ?? false;
    }

    public async Task SetMyOptInStatusAsync(bool showOnLeaderboard)
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to change this.");

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return;

        user.ShowOnLeaderboard = showOnLeaderboard;
        await userManager.UpdateAsync(user);
    }

    public async Task<BusinessImpactWidgetDto?> GetBusinessWidgetStatsAsync(Guid businessId)
    {
        var business = await businessRepository.GetByIdAsync(businessId);
        if (business is null || business.IsHidden || business.Status != BusinessStatuses.Approved)
            return null;

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var (orderedKg, monthKg, completedOrders) = await orderRepository.GetBusinessImpactStatsAsync(businessId, monthStart);
        var donatedKg = await packageRepository.GetDonatedWeightKgAsync(businessId);
        var totalKg = orderedKg + donatedKg;

        return new BusinessImpactWidgetDto(
            business.Id, business.Name, totalKg, monthKg, completedOrders,
            ImpactEquivalency.Co2eKg(totalKg), ImpactEquivalency.KmNotDriven(totalKg), ImpactEquivalency.LitersOfWater(totalKg));
    }
}
