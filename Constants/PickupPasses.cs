namespace Netrom_Eco_Meal.Constants;

// Bounds on OrderService.SplitPickupPassesAsync — a group order only realistically needs a
// handful of separate QR codes.
public static class PickupPasses
{
    public const int MinPasses = 1;
    public const int MaxPasses = 6;
}
