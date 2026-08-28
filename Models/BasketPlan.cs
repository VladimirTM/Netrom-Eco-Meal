using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Models;

// Result of IBasketPlannerAgent.ProposeBasketAsync — every Package/Quantity is re-validated
// server-side (BasketPlannerAgent.BuildValidatedPlan), so a bad model response can only
// narrow/drop items, never invent a package, its price, or its stock.
public record BasketPlan
{
    public required List<BasketPlanItem> Items { get; init; }
    public required decimal TotalPrice { get; init; }
    public required string Explanation { get; init; }
}

public record BasketPlanItem
{
    public required Package Package { get; init; }
    public required int Quantity { get; init; }
    public required string Reason { get; init; }
    public decimal LineTotal => Package.Price * Quantity;
}
