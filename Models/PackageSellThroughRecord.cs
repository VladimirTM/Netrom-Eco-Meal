namespace Netrom_Eco_Meal.Models;

// Compact, pre-fetched shape of a business's closed past package, handed to
// IMarkdownPricingAgent's get_sell_through_history tool — small/flat so the model reasons over
// just what it needs, same spirit as BasketPlannerAgent's PackageSearchResult.
public record PackageSellThroughRecord(
    string PackageName,
    Guid PackageTypeId,
    string PackageTypeName,
    decimal Price,
    int QuantitySold,
    int QuantityOffered,
    List<string> DietaryTags,
    int DaysAgo)
{
    public decimal SellThroughRate => QuantityOffered > 0 ? (decimal)QuantitySold / QuantityOffered : 0m;
}
