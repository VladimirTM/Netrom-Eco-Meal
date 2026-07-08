using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

public interface IPackageTypeService
{
    public Task<List<PackageType>> GetAllAsync();
}