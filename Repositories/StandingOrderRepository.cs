using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public class StandingOrderRepository(EcoMealDbContext context) : IStandingOrderRepository
{
    public async Task<List<StandingOrder>> GetByUserIdAsync(string userId)
    {
        return await context.StandingOrders
            .Include(s => s.Business)
            .Include(s => s.PackageType)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<StandingOrder?> GetByIdAsync(Guid id)
    {
        return await context.StandingOrders
            .Include(s => s.Business)
            .Include(s => s.PackageType)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<List<StandingOrder>> GetActiveByBusinessIdAsync(Guid businessId)
    {
        return await context.StandingOrders
            .Include(s => s.User)
            .Where(s => s.BusinessId == businessId && s.IsActive)
            .ToListAsync();
    }

    public async Task AddAsync(StandingOrder standingOrder)
    {
        await context.StandingOrders.AddAsync(standingOrder);
    }

    public async Task DeleteAsync(Guid id)
    {
        var standingOrder = await context.StandingOrders.FindAsync(id);
        if (standingOrder is null)
            return;
        context.StandingOrders.Remove(standingOrder);
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}
