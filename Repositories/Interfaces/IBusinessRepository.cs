using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public interface IBusinessRepository
{
    public Task<List<Business>> GetAllAsync();
    public Task<PaginatedList<Business>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessTypeId);
    public Task<Business?> GetByIdAsync(Guid id);
    public Task<Business?> GetByManagerIdAsync(string managerId);
    public Task AddAsync(Business business);
    public Task DeleteAsync(Guid id);
    public Task SaveChangesAsync();
}