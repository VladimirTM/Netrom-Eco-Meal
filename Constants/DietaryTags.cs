namespace Netrom_Eco_Meal.Constants;

// Free-form labels a manager can attach to a Package, shown as badges to customers.
public static class DietaryTags
{
    public const string Vegetarian = "Vegetarian";
    public const string Vegan = "Vegan";
    public const string GlutenFree = "Gluten-Free";
    public const string DairyFree = "Dairy-Free";
    public const string Halal = "Halal";
    public const string ContainsNuts = "Contains Nuts";
    public const string ContainsGluten = "Contains Gluten";
    public const string ContainsDairy = "Contains Dairy";

    public static readonly string[] All =
    [
        Vegetarian, Vegan, GlutenFree, DairyFree, Halal, ContainsNuts, ContainsGluten, ContainsDairy,
    ];

    // "Contains X" tags are allergen warnings, not dietary preferences — styled differently.
    public static readonly string[] Allergens = [ContainsNuts, ContainsGluten, ContainsDairy];

    public static bool IsAllergen(string tag) => Allergens.Contains(tag);
}
