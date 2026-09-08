namespace Netrom_Eco_Meal.Constants;

// Bounds on RescueCircleService.StartCircleAsync — mirrors PickupPasses' bounds, since a
// fully-paid circle ends up generating exactly one pickup pass per participant.
public static class RescueCircles
{
    public const int MinParticipants = 2;
    public const int MaxParticipants = 6;
}

// Values must match the RescueCircle.Status rows RescueCircleService writes.
public static class RescueCircleStatuses
{
    // Still collecting joins/payments — the underlying Order stays Pending until every slot is paid.
    public const string Open = "Open";
    // The organizer cancelled it, or it expired unpaid along with its Order — see
    // OrderService.RefundIfPaidAsync's Rescue Circle branch.
    public const string Cancelled = "Cancelled";
}
