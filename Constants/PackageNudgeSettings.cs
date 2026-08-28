namespace Netrom_Eco_Meal.Constants;

// Governs NearExpiryNudgeSweepService's periodic scan for packages closing soon with stock still
// unclaimed.
public static class PackageNudgeSettings
{
    // A package with stock left gets an AI-drafted nudge once its pickup window has this long left.
    public static readonly TimeSpan ClosingSoonWindow = TimeSpan.FromMinutes(30);

    // How often the sweep runs.
    public static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
}
