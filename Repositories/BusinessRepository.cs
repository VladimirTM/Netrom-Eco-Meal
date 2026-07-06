using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

public class BusinessRepository(EcoMealDbContext context) : IBusinessRepository
{
     public async Task<List<Business>> GetAllAsync()
    {
        return await context.Businesses.Include(b => b.BusinessType).ToListAsync();
    }
    
    public async Task<Business?> GetByIdAsync(Guid id)
    {
        return await context.Businesses.FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task AddAsync(Business business)
    {
        await context.Businesses.AddAsync(business);
    }
    
    public async Task DeleteAsync(Guid id)
    {
        var business = await context.Businesses.FindAsync(id);
        if (business is null)
            return;
        context.Businesses.Remove(business);
    }
    
    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}