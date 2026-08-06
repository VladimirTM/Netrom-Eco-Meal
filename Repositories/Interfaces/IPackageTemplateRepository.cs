using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public interface IPackageTemplateRepository
{
    public Task<List<PackageTemplate>> GetAllAsync();
    public Task<List<PackageTemplate>> GetByBusinessIdAsync(Guid businessId);
    public Task<List<PackageTemplate>> GetActiveAsync();
    public Task<PackageTemplate?> GetByIdAsync(Guid id);
    public Task AddAsync(PackageTemplate template);
    public Task DeleteAsync(Guid id);
    public Task SaveChangesAsync();
}
