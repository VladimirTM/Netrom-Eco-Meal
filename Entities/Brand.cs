namespace Netrom_Eco_Meal.Entities;

// Groups several Business rows (e.g. three branches of the same bakery) under one shared profile.
public class Brand
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ICollection<Business> Businesses { get; set; } = [];
}
