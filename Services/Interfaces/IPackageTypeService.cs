using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

// Reads are open to anyone (every package-browsing/form page needs the list); writes are
// admin-only, enforced in the implementation via CurrentUserAccessor.
public interface IPackageTypeService
{
    public Task<List<PackageType>> GetAllAsync();
    public Task AddAsync(PackageType packageType);
    public Task UpdateAsync(PackageType packageType);
    // Throws InvalidOperationException if a Package still references this type.
    public Task DeleteAsync(Guid id);
}
