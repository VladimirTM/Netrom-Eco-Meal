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
    public Task AddAsync(Package package);
    public Task DeleteAsync(Guid id);
    public Task SaveChangesAsync();
}