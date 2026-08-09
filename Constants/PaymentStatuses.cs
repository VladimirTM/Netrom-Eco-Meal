namespace Netrom_Eco_Meal.Constants;

public static class PaymentStatuses
{
    public const string Succeeded = "Succeeded";
    public const string Refunded = "Refunded";
    // Cancelled and a refund was attempted but Stripe rejected it — the charge is still with the
    // customer. Kept distinct from Succeeded so it isn't mistaken for "nothing happened".
    public const string RefundFailed = "RefundFailed";

    public static string Label(string status) => status switch
    {
        Refunded => "Refunded",
        RefundFailed => "Refund failed",
        _ => "Paid",
    };

    public static string BadgeClass(string status) => status switch
    {
        Refunded => "bg-secondary-subtle text-secondary",
        RefundFailed => "bg-danger-subtle text-danger",
        _ => "bg-success-subtle text-success",
    };

    public static string IconClass(string status) => status switch
    {
        Refunded => "bi-arrow-counterclockwise",
        RefundFailed => "bi-exclamation-triangle",
        _ => "bi-check-circle",
    };
}
