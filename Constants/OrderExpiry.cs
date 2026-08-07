namespace Netrom_Eco_Meal.Constants;

// Governs OrderLifecycleSweepService's periodic pass over time-based order transitions: stale
// PendingCheckout cleanup, stale Pending expiry, pickup reminders, and no-show detection.
public static class OrderExpiry
{
    // A Pending order not confirmed within this window releases its stock reservation.
    public static readonly TimeSpan PendingTimeout = TimeSpan.FromMinutes(30);

    // A Confirmed order gets a pickup reminder once its window has this long left to run.
    public static readonly TimeSpan PickupReminderLeadTime = TimeSpan.FromMinutes(30);

    // A PendingCheckout nobody returned to from Stripe (closed the tab, walked away) is swept up
    // after this long — the customer was never charged, so there's nothing to refund, just tidying.
    public static readonly TimeSpan PendingCheckoutTimeout = TimeSpan.FromMinutes(30);

    // How often the sweep runs.
    public static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
}
