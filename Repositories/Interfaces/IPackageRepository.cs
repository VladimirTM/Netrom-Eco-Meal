using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

public interface IPackageRepository
{
    public Task<List<Package>> GetAllAsync();
    public Task<Package?> GetByIdAsync(Guid id);
    public Task<List<Package>> GetByIdsAsync(IEnumerable<Guid> ids);
    public Task AddAsync(Package package);
    public Task DeleteAsync(Guid id);
    public Task SaveChangesAsync();
}