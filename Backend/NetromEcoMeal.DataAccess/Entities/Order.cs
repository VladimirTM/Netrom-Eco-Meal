using System.ComponentModel.DataAnnotations.Schema;

namespace NetromEcoMeal.Entities;

public class Order
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required Guid BusinessId { get; set; }
    public required Guid StatusId { get; set; }
    // Assigned by the order_numbers DB sequence on insert — never set this manually.
    public int OrderNumber { get; set; }
    // Used by the stale-Pending expiry sweep to decide when a reservation has gone unconfirmed too long.
    public DateTime CreatedAt { get; set; }
    // Set once OrderLifecycleSweepService sends the "pickup closes soon" reminder, so it isn't sent twice.
    public DateTime? PickupReminderSentAt { get; set; }
    // Free-text logistics note from the customer at checkout ("running late," "can't carry it to
    // my car"...) — shown to the business on /orders/manage. Null when left blank.
    public string? LogisticsNote { get; set; }
    [ForeignKey(nameof(UserId))]
    public required ApplicationUser User { get; set; }
    [ForeignKey(nameof(BusinessId))]
    public Business Business { get; set; } = null!;
    [ForeignKey(nameof(StatusId))]
    public Status Status { get; set; } = null!;
    public ICollection<OrderPackage> OrderPackages { get; set; } = [];
    // Null until CheckoutService.CompleteCheckoutAsync confirms the Stripe payment.
    public Payment? Payment { get; set; }
    // Created once the order is Confirmed (one by default) — see OrderService.ApplyStatusChangeAsync
    // and SplitPickupPassesAsync.
    public ICollection<OrderPickupPass> PickupPasses { get; set; } = [];
    // Set when this order was started as a shared Rescue Circle basket instead of a solo checkout —
    // see RescueCircleService. Null for every ordinary order.
    public RescueCircle? RescueCircle { get; set; }
}