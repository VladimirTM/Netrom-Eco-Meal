namespace Netrom_Eco_Meal.Constants;

// Throttles PlaceOrderAsync so one customer can't spam order creation.
public static class OrderRateLimit
{
    public const int MaxOrdersPerWindow = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
}
