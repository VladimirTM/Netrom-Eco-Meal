namespace NetromEcoMeal.Constants;

// Shared between PackageForm.tsx's validation and WebhookIntakeService (the app's other,
// unauthenticated entry point for creating a Package) so the two can't silently drift — a POS
// integration used to be able to post a package with no upper bound at all (e.g. a 999,999
// quantity), something a manager typing into the form could never do.
public static class PackageLimits
{
    public const decimal MinPrice = 0.01m;
    public const decimal MaxPrice = 10000m;
    public const int MaxQuantity = 1000;
    public const decimal MinWeightKg = 0.01m;
    public const decimal MaxWeightKg = 100m;
}
