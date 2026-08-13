using Microsoft.AspNetCore.Identity;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class ImpactService(
    IOrderRepository orderRepository,
    UserManager<ApplicationUser> userManager,
    CurrentUserAccessor currentUser) : IImpactService
{
    public async Task<List<LeaderboardEntry>> GetMonthlyLeaderboardAsync(int take = 20)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEndExclusive = monthStart.AddMonths(1);
        return await orderRepository.GetTopRescuersAsync(monthStart, monthEndExclusive, take);
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
}
