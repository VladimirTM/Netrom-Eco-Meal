using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public interface IPackageTypeRepository
{
    public Task<List<PackageType>> GetAllAsync();
    public Task<PackageType?> GetByIdAsync(Guid id);
    // Same cascade-delete landmine as IBusinessTypeRepository.IsInUseAsync — Package.PackageTypeId
    // is a required FK with no explicit OnDelete, so it defaults to Cascade.
    public Task<bool> IsInUseAsync(Guid id);
    public Task AddAsync(PackageType packageType);
    public Task DeleteAsync(PackageType packageType);
    public Task SaveChangesAsync();
}
