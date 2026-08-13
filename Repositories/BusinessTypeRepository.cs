using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public class BusinessTypeRepository(EcoMealDbContext context) : IBusinessTypeRepository
{
    public async Task<List<BusinessType>> GetAllAsync()
    {
        return await context.BusinessTypes.OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<BusinessType?> GetByIdAsync(Guid id)
    {
        return await context.BusinessTypes.FindAsync(id);
    }

    public async Task<bool> IsInUseAsync(Guid id)
    {
        return await context.Businesses.AnyAsync(b => b.BusinessTypeId == id);
    }

    public async Task AddAsync(BusinessType businessType)
    {
        await context.BusinessTypes.AddAsync(businessType);
    }

    public async Task DeleteAsync(BusinessType businessType)
    {
        context.BusinessTypes.Remove(businessType);
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}
