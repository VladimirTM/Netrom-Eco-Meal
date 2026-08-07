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
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        Guid pendingCheckoutId, string businessName, List<CheckoutLineItem> lines, string successUrl, string cancelUrl);
    Task<StripeSessionStatus> GetSessionStatusAsync(string sessionId);
    Task RefundAsync(string paymentIntentId);
}
