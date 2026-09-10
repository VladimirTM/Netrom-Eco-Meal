namespace Netrom_Eco_Meal.Services.Interfaces;

public record CheckoutLineItem(string PackageName, decimal UnitPrice, int Quantity);
public record CheckoutSessionResult(string SessionId, string Url);
public record StripeSessionStatus(bool IsPaid, string? PaymentIntentId, decimal AmountTotal, string Currency);

// Thin wrapper over the Stripe SDK — kept separate from ICheckoutService so OrderService (which
// needs to issue refunds on cancellation) doesn't have to depend on the order-creation side of
// checkout, and ICheckoutService (which needs to place orders) doesn't have to depend back on
// OrderService's refund path. Breaks what would otherwise be a circular DI dependency.
public interface IStripeGateway
{
    // discountAmount, when given, is applied as a one-off Stripe Coupon (Duration = "once") rather
    // than a negative line item — Stripe's price_data.unit_amount can't go negative.
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        Guid pendingCheckoutId, string businessName, List<CheckoutLineItem> lines, string successUrl, string cancelUrl,
        decimal? discountAmount = null, string? discountLabel = null);
    Task<StripeSessionStatus> GetSessionStatusAsync(string sessionId);
    Task RefundAsync(string paymentIntentId);
}
