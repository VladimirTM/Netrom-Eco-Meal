namespace Netrom_Eco_Meal.Models;

// One row of the /impact page's monthly leaderboard — only ever built from opted-in
// (ApplicationUser.ShowOnLeaderboard) users, see OrderRepository.GetTopRescuersAsync.
public record LeaderboardEntry(string UserId, string DisplayName, decimal KgSaved);
