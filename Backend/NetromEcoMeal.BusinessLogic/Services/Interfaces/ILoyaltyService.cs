namespace NetromEcoMeal.Services.Interfaces;

// A business's punch-card reward isn't a stored counter — it's recomputed on demand straight off
// Order.Status/CreatedAt, the same "no new tracking table" approach GetTotalKgSavedAsync uses.
public record LoyaltyProgress(int Threshold, decimal DiscountAmount, int CompletedThisMonth, int OrdersUntilReward);

public interface ILoyaltyService
{
    // Customer-only, current signed-in user. Null when this business has no punch card configured.
    public Task<LoyaltyProgress?> GetMyProgressAsync(Guid businessId);

    // System-facing (no current-user check) — CheckoutService calls this for the caller's own
    // checkout, already knowing who they are. Returns the discount to apply, or null/0 when no
    // reward is due this order.
    public Task<decimal?> EvaluateDiscountAsync(string userId, Guid businessId);
}
