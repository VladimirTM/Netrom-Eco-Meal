using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public class PackageRepository(EcoMealDbContext context) : IPackageRepository
{
    public async Task<List<Package>> GetAllAsync()
    {
        return await context.Packages.Include(p => p.PackageType).Include(p => p.Business).ToListAsync();
    }

    public async Task<Package?> GetByIdAsync(Guid id)
    {
        return await context.Packages.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Package>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        return await context.Packages
            .Include(p => p.PackageType)
            .Include(p => p.Business)
            .Where(p => idList.Contains(p.Id))
            .ToListAsync();
    }

    public async Task AddAsync(Package package)
    {
        await context.Packages.AddAsync(package);
    }
    
    public async Task DeleteAsync(Guid id)
    {
        var package = await context.Packages.FindAsync(id);
        if(package is null)
            return;
        context.Packages.Remove(package);
    }
    
    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}