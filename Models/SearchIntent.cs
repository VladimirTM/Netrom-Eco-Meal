namespace Netrom_Eco_Meal.Models;

// Structured shape ISearchIntentParser extracts a shopper's free-text query into and Home.razor
// applies against BusinessController.GetPagedAsync — the LLM only ever produces this
// (schema-constrained, then re-validated), so a bad extraction can narrow results but never
// fabricate one. DietaryTag is nulled out by SearchIntentParser unless it matches one of
// Constants.DietaryTags.All.
public record SearchIntent
{
    public string? Keywords { get; init; }
    public string? DietaryTag { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool ClosingSoon { get; init; }
    public bool NearMe { get; init; }
}
