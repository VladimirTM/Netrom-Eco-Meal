using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Services;

// Deliberately stateless — recomputes progress/discount from real Order rows on every call
// instead of a stored counter, so it can never drift (same choice GetTotalKgSavedAsync makes).
public class LoyaltyService(IOrderRepository orderRepository, IBusinessService businessService, ICurrentUser currentUser) : ILoyaltyService
{
    public async Task<LoyaltyProgress?> GetMyProgressAsync(Guid businessId)
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            return null;

        var business = await businessService.GetByIdAsync(businessId);
        if (business is not { LoyaltyPunchThreshold: { } threshold, LoyaltyDiscountAmount: { } discount })
            return null;

        var (rangeStart, rangeEndExclusive) = CurrentMonthUtc();
        var completedThisMonth = await orderRepository.GetCompletedOrderCountAsync(userId, businessId, rangeStart, rangeEndExclusive);
        var remainder = completedThisMonth % threshold;
        var ordersUntilReward = remainder == 0 ? threshold : threshold - remainder;

        return new LoyaltyProgress(threshold, discount, completedThisMonth, ordersUntilReward);
    }

    public async Task<decimal?> EvaluateDiscountAsync(string userId, Guid businessId)
    {
        var business = await businessService.GetByIdAsync(businessId);
        if (business is not { LoyaltyPunchThreshold: { } threshold, LoyaltyDiscountAmount: { } discount })
            return null;

        var (rangeStart, rangeEndExclusive) = CurrentMonthUtc();
        var completedThisMonth = await orderRepository.GetCompletedOrderCountAsync(userId, businessId, rangeStart, rangeEndExclusive);

        // The order this checkout is paying for would become completedThisMonth + 1 once picked
        // up — apply the reward the moment that would land on a threshold multiple.
        return (completedThisMonth + 1) % threshold == 0 ? discount : null;
    }

    private static (DateTime Start, DateTime EndExclusive) CurrentMonthUtc()
    {
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, start.AddMonths(1));
    }
}
