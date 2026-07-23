namespace Netrom_Eco_Meal.Constants;

// Governs the background sweep that auto-cancels stale Pending orders (see PendingOrderExpiryService).
public static class OrderExpiry
{
    // A Pending order not confirmed within this window releases its stock reservation.
    public static readonly TimeSpan PendingTimeout = TimeSpan.FromMinutes(30);

    // How often the sweep runs.
    public static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
}
