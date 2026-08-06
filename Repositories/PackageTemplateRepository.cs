using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public class PackageTemplateRepository(EcoMealDbContext context) : IPackageTemplateRepository
{
    public async Task<List<PackageTemplate>> GetAllAsync()
    {
        return await context.PackageTemplates.Include(t => t.PackageType).Include(t => t.Business)
            .OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<List<PackageTemplate>> GetByBusinessIdAsync(Guid businessId)
    {
        return await context.PackageTemplates.Include(t => t.PackageType).Include(t => t.Business)
            .Where(t => t.BusinessId == businessId).OrderBy(t => t.Name).ToListAsync();
    }

    // Used by PackageTemplateGenerationService — no need to include navigation properties.
    public async Task<List<PackageTemplate>> GetActiveAsync()
    {
        return await context.PackageTemplates.Where(t => t.IsActive).ToListAsync();
    }

    public async Task<PackageTemplate?> GetByIdAsync(Guid id)
    {
        return await context.PackageTemplates.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task AddAsync(PackageTemplate template)
    {
        await context.PackageTemplates.AddAsync(template);
    }

    public async Task DeleteAsync(Guid id)
    {
        var template = await context.PackageTemplates.FindAsync(id);
        if (template is null)
            return;
        context.PackageTemplates.Remove(template);
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}
