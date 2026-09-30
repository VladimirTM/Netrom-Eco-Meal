using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Services;

// Deliberately stateless — recomputes the streak from real Order rows on every call instead of a
// stored counter, so it can never drift (same choice LoyaltyService/GetTotalKgSavedAsync make).
public class StreakService(IOrderRepository orderRepository, ICurrentUser currentUser) : IStreakService
{
    public async Task<int> GetStreakWeeksAsync(string userId)
    {
        var dates = await orderRepository.GetCompletedOrderCreatedDatesAsync(userId);
        if (dates.Count == 0)
            return 0;

        var weekStarts = dates.Select(WeekStart).ToHashSet();
        var cursor = WeekStart(DateTime.UtcNow);

        // This week not having a Completed order yet doesn't break the streak — give it the
        // benefit of the doubt and start checking from last week instead.
        if (!weekStarts.Contains(cursor))
            cursor = cursor.AddDays(-7);

        var streak = 0;
        while (weekStarts.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-7);
        }

        return streak;
    }

    public async Task<int> GetMyStreakWeeksAsync()
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        return userId is null ? 0 : await GetStreakWeeksAsync(userId);
    }

    private static DateTime WeekStart(DateTime dt)
    {
        var daysSinceMonday = ((int)dt.DayOfWeek + 6) % 7;
        return dt.Date.AddDays(-daysSinceMonday);
    }
}
