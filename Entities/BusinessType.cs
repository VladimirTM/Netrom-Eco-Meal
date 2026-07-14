namespace Netrom_Eco_Meal.Entities;

// Lookup table (Restaurant, Bakery, ...) seeded by DbSeeder.
public class BusinessType
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
}