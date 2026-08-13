using Netrom_Eco_Meal.Models;

namespace Netrom_Eco_Meal.Services.Interfaces;

// Backs the public /impact leaderboard. The leaderboard read itself needs no auth (same "public
// aggregate, no per-user data exposed" reasoning as OrderService.GetTotalKgSavedAsync) — only
// the opt-in write touches the current signed-in user.
public interface IImpactService
{
    public Task<List<LeaderboardEntry>> GetMonthlyLeaderboardAsync(int take = 20);
    public Task<bool> GetMyOptInStatusAsync();
    public Task SetMyOptInStatusAsync(bool showOnLeaderboard);
}
