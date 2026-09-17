using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

// AddAsync only stages the change — call SaveChangesAsync to persist.
public class StoreCreditRepository(EcoMealDbContext context) : IStoreCreditRepository
{
    public async Task<decimal> GetBalanceAsync(string userId)
    {
        return await context.StoreCreditEntries.Where(e => e.UserId == userId).SumAsync(e => (decimal?)e.Amount) ?? 0m;
    }

    public async Task<List<StoreCreditEntry>> GetByUserIdAsync(string userId)
    {
        return await context.StoreCreditEntries
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(StoreCreditEntry entry)
    {
        await context.StoreCreditEntries.AddAsync(entry);
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}
