using Netrom_Eco_Meal.Models;

namespace Netrom_Eco_Meal.Services.Interfaces;

// Backs the public /impact leaderboard and the public per-business impact widget. Every read here
// needs no auth (same "public aggregate, no per-user data exposed" reasoning as OrderService
// .GetTotalKgSavedAsync) — only the opt-in write touches the current signed-in user.
public interface IImpactService
{
    public Task<List<LeaderboardEntry>> GetMonthlyLeaderboardAsync(int take = 20);
    public Task<bool> GetMyOptInStatusAsync();
    public Task SetMyOptInStatusAsync(bool showOnLeaderboard);
    // null if the business doesn't exist, isn't Approved, or is moderator-hidden — same
    // live-storefront visibility gate the app itself uses. Backs ImpactController's public HTTP
    // GET for wwwroot/js/impact-widget.js.
    public Task<BusinessImpactWidgetDto?> GetBusinessWidgetStatsAsync(Guid businessId);
}
