namespace NetromEcoMeal.Constants;

// Amounts granted by ReferralService.TryRewardFirstCompletionAsync — a ledger entry only, applied
// at checkout the same way LoyaltyService's punch-card reward is (a Stripe Coupon, never a payout).
public static class ReferralCredit
{
    public const decimal ReferrerAmount = 15m;
    public const decimal RefereeAmount = 10m;
}
