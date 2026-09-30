using Microsoft.EntityFrameworkCore;
using NetromEcoMeal.Database;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories.Interfaces;

namespace NetromEcoMeal.Repositories;

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
