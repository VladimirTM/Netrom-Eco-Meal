namespace Netrom_Eco_Meal.Constants;

// Values must match the Status.Name rows DbSeeder creates.
public static class OrderStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    // A Confirmed order whose pickup window closed without being completed — set by the manager
    // manually or by OrderLifecycleSweepService's automatic sweep.
    public const string NoShow = "NoShow";
}
