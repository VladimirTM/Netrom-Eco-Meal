using NetromEcoMeal.Entities;

namespace NetromEcoMeal.Repositories.Interfaces;

// AddAsync only stages the change — call SaveChangesAsync to persist.
public interface IReferralRepository
{
    // A referred user can only ever appear once (unique index) — null once already rewarded.
    public Task<Referral?> GetPendingByReferredUserIdAsync(string referredUserId);
    // Everyone the given user has successfully invited, newest first — feeds /referrals.
    public Task<List<Referral>> GetByReferrerUserIdAsync(string referrerUserId);
    public Task AddAsync(Referral referral);
    public Task SaveChangesAsync();
}
