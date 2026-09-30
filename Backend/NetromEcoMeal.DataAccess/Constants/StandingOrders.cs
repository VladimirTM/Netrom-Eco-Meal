namespace NetromEcoMeal.Constants;

// Governs StandingOrderService — "auto-reserve my usual from this kitchen whenever it goes live".
public static class StandingOrders
{
    public const decimal MinWeeklySpend = 1m;
    public const decimal MaxWeeklySpend = 2000m;

    // The rolling window a standing order's own spend is measured against — "up to X lei/week".
    public static readonly TimeSpan SpendWindow = TimeSpan.FromDays(7);
}
