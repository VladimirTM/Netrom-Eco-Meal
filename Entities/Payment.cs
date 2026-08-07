using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

// One row per Order — created once its Stripe Checkout Session is confirmed paid. Never deleted;
// a cancelled/no-show order's payment is marked Refunded (or left Succeeded for a no-show, which
// is what makes the no-show fee "real" — the charge is simply never reversed).
public class Payment
{
    public Guid Id { get; set; }
    public required Guid OrderId { get; set; }
    public required decimal Amount { get; set; }
    // Lowercase ISO currency code, as Stripe returns it (e.g. "ron").
    public required string Currency { get; set; }
    public required string StripeCheckoutSessionId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    // See Constants.PaymentStatuses.
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; } = null!;
}
