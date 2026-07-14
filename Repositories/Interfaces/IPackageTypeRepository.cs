using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

// Read-only lookup over the seeded PackageType list.
public interface IPackageTypeRepository
{
    public Task<List<PackageType>> GetAllAsync();
}