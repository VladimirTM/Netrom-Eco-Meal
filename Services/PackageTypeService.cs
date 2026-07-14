using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

// Thin pass-through — lookup data has no business rules to enforce.
public class PackageTypeService(IPackageTypeRepository packageTypeRepository) : IPackageTypeService
{
    public async Task<List<PackageType>> GetAllAsync()
    {
        return await packageTypeRepository.GetAllAsync();
    }
}