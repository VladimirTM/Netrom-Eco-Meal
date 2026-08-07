using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

public record CheckoutCompletionResult(bool Success, string Message, Order? Order, decimal? KgSaved);

// Owns the "pay before the order exists" flow: StartCheckoutAsync parks the cart in a
// PendingCheckout and sends the customer to Stripe; CompleteCheckoutAsync resolves the redirect
// back, confirms payment, and only then calls IOrderService.PlaceOrderAsync.
public interface ICheckoutService
{
    Task<string> StartCheckoutAsync(Guid businessId, List<OrderLineRequest> lines);
    Task<CheckoutCompletionResult> CompleteCheckoutAsync(Guid pendingCheckoutId, string sessionId);
    // System-triggered — called by the background sweep to clean up checkouts nobody returned to.
    Task<int> ExpireStalePendingCheckoutsAsync();
}
