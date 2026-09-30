namespace NetromEcoMeal.Entities;

// Bridges "customer clicked Pay" to "Order actually exists" — the cart's lines are parked here
// while the customer is off on Stripe's hosted Checkout page, since an Order (and its stock
// reservation) shouldn't exist until payment is confirmed. Consumed exactly once, on the redirect
// back from Stripe; CheckoutService.ExpireStalePendingCheckoutsAsync sweeps up abandoned ones
// nobody ever returns to.
public class PendingCheckout
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required Guid BusinessId { get; set; }
    // Serialized List<OrderLineRequest> — the cart contents at the moment checkout started.
    public required string LinesJson { get; set; }
    // Carries Order.LogisticsNote across the round trip to Stripe and back, so
    // CompleteCheckoutAsync can set it on the Order it places.
    public string? LogisticsNote { get; set; }
    // How much store credit StartCheckoutAsync folded into the Stripe discount, if any — carried
    // across the round trip so CompleteCheckoutAsync debits exactly what was actually offered,
    // not whatever the balance happens to be by the time Stripe redirects back.
    public decimal? CreditApplied { get; set; }
    public string? StripeCheckoutSessionId { get; set; }
    public DateTime CreatedAt { get; set; }
    // Set once CheckoutService.CompleteCheckoutAsync has resolved this one way or another —
    // guards against double-spending a single Stripe payment into two orders on a page refresh.
    public DateTime? ConsumedAt { get; set; }
    // Null even after ConsumedAt is set if the paid-for order couldn't actually be placed
    // (e.g. stock vanished in the meantime) and the payment was refunded instead.
    public Guid? ResultingOrderId { get; set; }
}
