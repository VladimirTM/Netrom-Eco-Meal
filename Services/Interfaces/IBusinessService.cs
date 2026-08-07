using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;

namespace Netrom_Eco_Meal.Services.Interfaces;

// Create/delete are admin-only; update is admin or one of the business's own staff.
public interface IBusinessService
{
    public Task<List<Business>> GetAllAsync();
    public Task<PaginatedList<Business>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessTypeId, string? staffUserId = null, string? sortBy = null, bool favoritesOnly = false, double? customerLat = null, double? customerLng = null);
    public Task<Business?> GetByIdAsync(Guid id);
    public Task<List<Business>> GetByStaffUserIdAsync(string userId);
    public Task<List<ApplicationUser>> GetStaffAsync(Guid businessId);
    public Task<bool> IsStaffAsync(Guid businessId, string userId);
    public Task AddAsync(Business business);
    public Task UpdateAsync(Business business);
    public Task DeleteAsync(Business business);
    public Task<bool> AddStaffAsync(Guid businessId, string userId);
    public Task<bool> RemoveStaffAsync(Guid businessId, string userId);
}