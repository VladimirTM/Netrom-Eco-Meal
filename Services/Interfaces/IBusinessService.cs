using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

// Create/delete are admin-only; update is admin or the business's own manager.
public interface IBusinessService
{
    public Task<List<Business>> GetAllAsync();
    public Task<Business?> GetByIdAsync(Guid id);
    public Task<Business?> GetByManagerIdAsync(string managerId);
    public Task AddAsync(Business business);
    public Task UpdateAsync(Business business);
    public Task DeleteAsync(Business business);
    public Task<bool> AssignManagerAsync(Guid businessId, string? managerId);
}