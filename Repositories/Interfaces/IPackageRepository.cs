using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public interface IPackageRepository
{
    public Task<List<Package>> GetAllAsync();
    public Task<PaginatedList<Package>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessId, Guid? packageTypeId);
    public Task<Package?> GetByIdAsync(Guid id);
    public Task<List<Package>> GetByIdsAsync(IEnumerable<Guid> ids);
    // Name-only — for callers (e.g. ReportService) that just need display names for a batch of ids.
    public Task<Dictionary<Guid, string>> GetNamesByIdsAsync(IEnumerable<Guid> ids);
    // Includes the OrderPackages/Order/Status graph so analytics can aggregate without a second round-trip.
    public Task<List<Package>> GetForAnalyticsAsync(Guid? businessId, DateTime since);
    public Task AddAsync(Package package);
    public Task DeleteAsync(Guid id);
    public Task SaveChangesAsync();
}