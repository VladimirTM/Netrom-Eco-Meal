namespace NetromEcoMeal.Constants;

// Turns OrderService.GetTotalKgSavedAsync's one real number into a picturable equivalency for the
// order receipt/`/impact` page. Round, published planet-scale averages — not a precise LCA.
public static class ImpactEquivalency
{
    // Average CO2e per kg of food waste, across categories (FAO/WRAP-style estimates).
    public const decimal Co2eKgPerKgFood = 2.5m;

    // Average embedded ("virtual") water footprint per kg of food produced.
    public const decimal WaterLitersPerKgFood = 1000m;

    // Average passenger car tailpipe emissions per km.
    public const decimal CarCo2eKgPerKm = 0.19m;

    public static decimal Co2eKg(decimal kgSaved) => kgSaved * Co2eKgPerKgFood;
    public static decimal LitersOfWater(decimal kgSaved) => kgSaved * WaterLitersPerKgFood;
    public static decimal KmNotDriven(decimal kgSaved) => Co2eKg(kgSaved) / CarCo2eKgPerKm;
}
