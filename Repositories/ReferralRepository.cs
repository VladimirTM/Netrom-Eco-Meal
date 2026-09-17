using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

// AddAsync only stages the change — call SaveChangesAsync to persist.
public class ReferralRepository(EcoMealDbContext context) : IReferralRepository
{
    public async Task<Referral?> GetPendingByReferredUserIdAsync(string referredUserId)
    {
        return await context.Referrals.FirstOrDefaultAsync(r => r.ReferredUserId == referredUserId && r.RewardedAt == null);
    }

    public async Task<List<Referral>> GetByReferrerUserIdAsync(string referrerUserId)
    {
        return await context.Referrals
            .Include(r => r.Referred)
            .Where(r => r.ReferrerUserId == referrerUserId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(Referral referral)
    {
        await context.Referrals.AddAsync(referral);
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}
