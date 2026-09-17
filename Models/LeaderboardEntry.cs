namespace Netrom_Eco_Meal.Models;

// One row of the /impact page's monthly leaderboard — only ever built from opted-in
// (ApplicationUser.ShowOnLeaderboard) users, see OrderRepository.GetTopRescuersAsync.
// StreakWeeks is filled in afterwards by ImpactService (see IStreakService) — always 0 straight
// off the repository query.
public record LeaderboardEntry(string UserId, string DisplayName, decimal KgSaved, int StreakWeeks = 0);
