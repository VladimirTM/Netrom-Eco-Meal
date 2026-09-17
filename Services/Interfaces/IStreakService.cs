namespace Netrom_Eco_Meal.Services.Interfaces;

public interface IStreakService
{
    // Consecutive Monday-start weeks with at least one Completed order, ending at the most recent
    // one that has it — an in-progress current week never breaks the streak. Recomputed on demand,
    // same reasoning as LoyaltyService/GetTotalKgSavedAsync.
    public Task<int> GetStreakWeeksAsync(string userId);

    // Current signed-in user only. Zero (not an error) when signed out.
    public Task<int> GetMyStreakWeeksAsync();
}
