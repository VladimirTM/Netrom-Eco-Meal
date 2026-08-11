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
    public Task<decimal> GetTotalWeightSavedKgAsync();
    // Pending orders past their confirm window or a closed pickup slot — feeds the expiry sweep.
    public Task<List<Order>> GetStalePendingOrdersAsync(DateTime createdBefore);
    // Confirmed orders whose pickup window has fully closed — feeds the no-show sweep.
    public Task<List<Order>> GetOverduePickupOrdersAsync(DateTime now);
    // Confirmed orders whose pickup window closes within the lead time and haven't been reminded yet.
    public Task<List<Order>> GetPickupReminderCandidatesAsync(DateTime remindBy, DateTime now);
    // Sum of Pending reservations per package — Package.Quantity alone overstates what's still bookable.
    public Task<Dictionary<Guid, int>> GetPendingQuantitiesByPackageIdsAsync(IEnumerable<Guid> packageIds);
    public Task AddAsync(Order order);
    public Task DeleteAsync(Guid id);
    public Task SaveChangesAsync();
}
