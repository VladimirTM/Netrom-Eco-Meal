using Microsoft.EntityFrameworkCore;
using NetromEcoMeal.Database;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories.Interfaces;

namespace NetromEcoMeal.Repositories;

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
