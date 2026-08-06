namespace Netrom_Eco_Meal.Constants;

// Governs PackageTemplateGenerationService's periodic pass over active recurring templates.
public static class PackageTemplateGeneration
{
    // How often the sweep checks for templates that haven't generated today's instance yet.
    public static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(15);
}
