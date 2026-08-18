using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;

namespace Netrom_Eco_Meal.Services.Interfaces;

// Create/delete are admin-only; update is admin or one of the business's own staff. Apply is
// open to any signed-in user (self-service signup); Approve/Reject/Hide/Unhide are admin-only.
public interface IBusinessService
{
    public Task<List<Business>> GetAllAsync(bool publicOnly = false);
    public Task<PaginatedList<Business>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessTypeId, string? staffUserId = null, string? sortBy = null, bool favoritesOnly = false, double? customerLat = null, double? customerLng = null, string? statusFilter = null, bool publicOnly = false, string? dietaryTag = null, decimal? maxPrice = null);
    public Task<Business?> GetByIdAsync(Guid id);
    public Task<Dictionary<Guid, string>> GetNamesByIdsAsync(IEnumerable<Guid> ids);
    public Task<List<Business>> GetByStaffUserIdAsync(string userId);
    public Task<List<ApplicationUser>> GetStaffAsync(Guid businessId);
    public Task<bool> IsStaffAsync(Guid businessId, string userId);
    public Task AddAsync(Business business);
    public Task UpdateAsync(Business business);
    public Task DeleteAsync(Business business);
    public Task<bool> AddStaffAsync(Guid businessId, string userId, string? userName = null);
    public Task<bool> RemoveStaffAsync(Guid businessId, string userId, string? userName = null);

    // Self-service business signup — creates a PendingApproval business owned by the caller.
    public Task<Business> ApplyAsync(Business business);
    public Task ApproveAsync(Guid businessId);
    public Task RejectAsync(Guid businessId, string reason);
    // notify: false lets a caller (see ReportService.TakeActionAsync) defer the staff
    // notification fan-out until after its own transaction commits, so outbound push calls
    // don't hold DB locks open. Returns the hidden business (null if it was a no-op) so the
    // caller has what NotifyHiddenAsync needs.
    public Task<Business?> HideAsync(Guid businessId, string reason, bool notify = true);
    public Task NotifyHiddenAsync(Business business, string reason);
    public Task UnhideAsync(Guid businessId);

    // Admin or one of the business's own staff, same as UpdateAsync.
    public Task SetHoursAsync(Guid businessId, List<BusinessHours> hours);
    public Task<BusinessClosure> AddClosureAsync(Guid businessId, DateOnly startDate, DateOnly endDate, string? reason);
    public Task<bool> RemoveClosureAsync(Guid businessId, Guid closureId);
}
