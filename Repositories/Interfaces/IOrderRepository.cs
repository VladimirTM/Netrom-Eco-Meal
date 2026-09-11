using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public interface IOrderRepository
{
    public Task<List<Order>> GetAllAsync();
    public Task<List<Order>> GetByUserIdAsync(string userId);
    public Task<List<Order>> GetByBusinessIdAsync(Guid businessId);
    public Task<PaginatedList<Order>> GetPagedByUserIdAsync(string userId, int pageIndex, int pageSize, string? status);
    public Task<PaginatedList<Order>> GetPagedForManagementAsync(int pageIndex, int pageSize, string? search, Guid? businessId, string? status);
    // Unpaginated, date-bounded — feeds both CSV export and the dashboard trend chart.
    public Task<List<Order>> GetInRangeAsync(Guid? businessId, DateTime? from, DateTime? to);
    public Task<Order?> GetByIdAsync(Guid id);
    public Task<bool> HasCompletedOrderAsync(string userId, Guid businessId);
    // Distinct packages a customer has a Completed order for at a business — feeds the "which
    // package is this review about" picker.
    public Task<List<Package>> GetCompletedPackagesAsync(string userId, Guid businessId);
    // Distinct customers with a Completed order at a business — feeds NearExpiryNudgeService's
    // audience, alongside IFavoriteRepository.GetFavoritingUsersAsync.
    public Task<List<ApplicationUser>> GetPastCustomersAsync(Guid businessId);
    public Task<decimal> GetTotalWeightSavedKgAsync();
    // All-time and this-month kg saved plus a Completed-order count for one business —
    // order-pickups only; ImpactService adds donated weight on top. Feeds the business impact widget.
    public Task<(decimal TotalKg, decimal MonthKg, int CompletedOrders)> GetBusinessImpactStatsAsync(Guid businessId, DateTime monthStart);
    // Top opted-in (ApplicationUser.ShowOnLeaderboard) rescuers by kg saved within [from, toExclusive)
    // — feeds the /impact page. A user who never opted in never appears, full stop, not as an
    // anonymized row.
    public Task<List<LeaderboardEntry>> GetTopRescuersAsync(DateTime rangeStart, DateTime rangeEndExclusive, int take);
    // Pending orders past their confirm window or a closed pickup slot — feeds the expiry sweep.
    public Task<List<Order>> GetStalePendingOrdersAsync(DateTime createdBefore);
    // Confirmed orders whose pickup window has fully closed — feeds the no-show sweep.
    public Task<List<Order>> GetOverduePickupOrdersAsync(DateTime now);
    // Confirmed orders whose pickup window closes within the lead time and haven't been reminded yet.
    public Task<List<Order>> GetPickupReminderCandidatesAsync(DateTime remindBy, DateTime now);
    // Sum of Pending reservations per package — Package.Quantity alone overstates what's still bookable.
    public Task<Dictionary<Guid, int>> GetPendingQuantitiesByPackageIdsAsync(IEnumerable<Guid> packageIds);
    // Completed orders a customer placed at a business within [rangeStart, rangeEndExclusive) —
    // feeds LoyaltyService's punch-card progress/discount calculation.
    public Task<int> GetCompletedOrderCountAsync(string userId, Guid businessId, DateTime rangeStart, DateTime rangeEndExclusive);
    // Total spend at a business in range, excluding Cancelled (refunded, so it never really cost
    // anything) — feeds StandingOrderService's weekly budget check.
    public Task<decimal> GetSpendInRangeAsync(string userId, Guid businessId, Guid? packageTypeId, string? dietaryTag, DateTime rangeStart, DateTime rangeEndExclusive);
    public Task AddAsync(Order order);
    public Task DeleteAsync(Guid id);
    public Task SaveChangesAsync();
}
