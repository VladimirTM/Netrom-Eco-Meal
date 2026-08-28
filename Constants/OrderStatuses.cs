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

    // The stored value has to stay a single PascalCase word (matches Status.Name seed rows and
    // CSS class names like order-status-noshow), but "NoShow" reads as one run-on word wherever
    // it's shown to a customer or manager — this is the display-only fix-up for that one case.
    public static string Label(string status) => status == NoShow ? "No-show" : status;
}
