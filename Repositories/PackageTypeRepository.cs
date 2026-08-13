using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public class PackageTypeRepository(EcoMealDbContext context) : IPackageTypeRepository
{
    public async Task<List<PackageType>> GetAllAsync()
    {
        return await context.PackageTypes.OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<PackageType?> GetByIdAsync(Guid id)
    {
        return await context.PackageTypes.FindAsync(id);
    }

    public async Task<bool> IsInUseAsync(Guid id)
    {
        return await context.Packages.AnyAsync(p => p.PackageTypeId == id);
    }

    public async Task AddAsync(PackageType packageType)
    {
        await context.PackageTypes.AddAsync(packageType);
    }

    public async Task DeleteAsync(PackageType packageType)
    {
        context.PackageTypes.Remove(packageType);
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}
