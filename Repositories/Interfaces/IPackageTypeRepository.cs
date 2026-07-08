using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

public interface IPackageTypeRepository
{
    public Task<List<PackageType>> GetAllAsync();
}