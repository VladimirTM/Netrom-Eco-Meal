namespace Netrom_Eco_Meal.Entities;

// Lookup table for order status; see Constants.OrderStatuses for the fixed values.
public class Status
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
}