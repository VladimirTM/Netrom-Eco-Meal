using System.ComponentModel.DataAnnotations.Schema;

namespace NetromEcoMeal.Entities;

// A store-credit ledger row — positive Amount grants credit (a referral bonus), negative spends it
// (applied to a checkout). A user's balance is the sum of their rows, recomputed on demand rather
// than tracked as a separate counter, the same "can't drift" approach LoyaltyService/
// GetTotalKgSavedAsync already use. Deliberately never a real payout — only ever redeemed as a
// Stripe Coupon amount at checkout (see CheckoutService.StartCheckoutAsync), so it never touches
// the Stripe integration's actual money-movement trust boundary.
public class StoreCreditEntry
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required decimal Amount { get; set; }
    public required string Reason { get; set; }
    // Set only on a spend row (negative Amount) — which checkout it was applied to.
    public Guid? RelatedOrderId { get; set; }
    public DateTime CreatedAt { get; set; }
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;
}
