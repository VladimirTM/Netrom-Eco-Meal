using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

public class PackageTypeRepository(EcoMealDbContext context) : IPackageTypeRepository
{
    public async Task<List<PackageType>> GetAllAsync()
    {
        return await context.PackageTypes.ToListAsync();
    }
}