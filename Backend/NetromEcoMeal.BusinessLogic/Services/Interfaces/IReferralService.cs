namespace NetromEcoMeal.Services.Interfaces;

public record ReferralRow(string FriendName, DateTime InvitedAt, bool Rewarded);

// A user's own referral code/link plus their store-credit balance and invite history — feeds /referrals.
public record ReferralInfo(string ReferralCode, decimal CreditBalance, List<ReferralRow> Referrals);

public interface IReferralService
{
    // Current signed-in user only — generates and persists a ReferralCode on first call if the
    // account doesn't have one yet (self-registered accounts predating this feature, or seeded
    // demo accounts).
    public Task<ReferralInfo> GetMyReferralInfoAsync();

    // Best-effort: an unknown/blank code is silently ignored rather than failing registration —
    // a typo'd referral code shouldn't block someone from creating an account.
    public Task RegisterReferralAsync(string? referralCode, string referredUserId);

    // Called from OrderService right after an order reaches Completed. No-ops unless this is
    // genuinely the referred user's first ever completed order and they haven't already been
    // rewarded for it.
    public Task TryRewardFirstCompletionAsync(string userId);

    // System-facing (no current-user check) — CheckoutService calls this for the caller's own
    // checkout. Returns the credit actually available to spend right now.
    public Task<decimal> GetAvailableBalanceAsync(string userId);

    // Debits exactly `amount` from the user's balance as a checkout spend — CheckoutService calls
    // this once Stripe confirms payment, never before.
    public Task DebitAsync(string userId, decimal amount, Guid orderId);
}
