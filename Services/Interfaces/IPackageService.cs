using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

// Write methods are restricted to admins and the package's own business manager.
public interface IPackageService
{
    public Task<List<Package>> GetAllAsync();
    public Task<Package?> GetByIdAsync(Guid id);
    public Task<List<Package>> GetByIdsAsync(IEnumerable<Guid> ids);
    public Task AddAsync(Package package);
    public Task UpdateAsync(Package package);
    public Task DeleteAsync(Package package);
}