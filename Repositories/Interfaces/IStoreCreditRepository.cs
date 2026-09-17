using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

// AddAsync only stages the change — call SaveChangesAsync to persist.
public interface IStoreCreditRepository
{
    // Sum of every entry for a user — recomputed on demand rather than a stored counter, same
    // "can't drift" reasoning as LoyaltyService/GetTotalKgSavedAsync.
    public Task<decimal> GetBalanceAsync(string userId);
    // Full ledger, newest first — feeds /referrals' history list.
    public Task<List<StoreCreditEntry>> GetByUserIdAsync(string userId);
    public Task AddAsync(StoreCreditEntry entry);
    public Task SaveChangesAsync();
}
