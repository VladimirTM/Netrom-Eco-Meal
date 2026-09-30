using NetromEcoMeal.Entities;

namespace NetromEcoMeal.Services.Interfaces;

// Reads are open to anyone (every package-browsing/form page needs the list); writes are
// admin-only, enforced in the implementation via ICurrentUser.
public interface IPackageTypeService
{
    public Task<List<PackageType>> GetAllAsync();
    public Task AddAsync(PackageType packageType);
    public Task UpdateAsync(PackageType packageType);
    // Throws InvalidOperationException if a Package still references this type.
    public Task DeleteAsync(Guid id);
}
