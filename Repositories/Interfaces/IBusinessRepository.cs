using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public interface IBusinessRepository
{
    public Task<List<Business>> GetAllAsync(bool publicOnly = false);
    public Task<PaginatedList<Business>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessTypeId, string? staffUserId = null, string? sortBy = null, string? favoritedByUserId = null, double? customerLat = null, double? customerLng = null, string? statusFilter = null, bool publicOnly = false);
    public Task<Business?> GetByIdAsync(Guid id);
    // Name-only, no Include — for callers (e.g. ReportService) that just need display names for
    // a batch of ids and shouldn't pay for GetByIdAsync's Staff/Hours/Closures split query.
    public Task<Dictionary<Guid, string>> GetNamesByIdsAsync(IEnumerable<Guid> ids);
    public Task<List<Business>> GetByStaffUserIdAsync(string userId);
    public Task<List<ApplicationUser>> GetStaffAsync(Guid businessId);
    public Task<bool> IsStaffAsync(Guid businessId, string userId);
    public Task<bool> AddStaffAsync(Guid businessId, string userId);
    public Task<bool> RemoveStaffAsync(Guid businessId, string userId);
    public Task AddAsync(Business business);
    public Task DeleteAsync(Guid id);
    public Task SaveChangesAsync();

    // Replaces the business's full set of weekly-hours rows (up to one per DayOfWeek) in one go —
    // there's no partial add/remove for hours, only "here's this week's schedule now."
    public Task SetHoursAsync(Guid businessId, List<BusinessHours> hours);
    public Task<BusinessClosure> AddClosureAsync(BusinessClosure closure);
    public Task<bool> RemoveClosureAsync(Guid businessId, Guid closureId);
}
