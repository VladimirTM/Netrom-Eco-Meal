using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class PackageTypeService(IPackageTypeRepository packageTypeRepository) : IPackageTypeService
{
    public async Task<List<PackageType>> GetAllAsync()
    {
        return await packageTypeRepository.GetAllAsync();
    }
}