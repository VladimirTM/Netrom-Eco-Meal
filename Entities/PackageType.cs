namespace Netrom_Eco_Meal.Entities;

// Lookup table (Surprise Bag, Meal Box, ...) — seeded by DbSeeder, admin-manageable at /types.
public class PackageType
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
}