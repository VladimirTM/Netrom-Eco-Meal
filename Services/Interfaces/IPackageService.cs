using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;

namespace Netrom_Eco_Meal.Services.Interfaces;

// Write methods are restricted to admins and the package's own business manager.
public interface IPackageService
{
    public Task<List<Package>> GetAllAsync();
    public Task<PaginatedList<Package>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessId, Guid? packageTypeId);
    public Task<Package?> GetByIdAsync(Guid id);
    public Task AddAsync(Package package);
    public Task UpdateAsync(Package package);
    public Task DeleteAsync(Package package);

    // Bulk actions for the /packages multi-select toolbar — same per-business ownership check as the write methods above.
    public Task<List<Package>> DuplicateManyAsync(List<Guid> packageIds);
    public Task AdjustQuantityManyAsync(List<Guid> packageIds, int delta);
    public Task ExtendPickupWindowManyAsync(List<Guid> packageIds, TimeSpan extension);

    // Raw package + order graph behind the Dashboard's business analytics card — aggregated
    // client-side since it needs the viewer's local timezone for hour bucketing.
    public Task<List<Package>> GetForAnalyticsAsync(Guid? businessId, DateTime since);
}