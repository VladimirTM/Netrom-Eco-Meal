namespace Netrom_Eco_Meal.Entities;

// Lookup table (Surprise Bag, Meal Box, ...) seeded by DbSeeder.
public class PackageType
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
}