namespace Netrom_Eco_Meal.Models;

// Result of IMarkdownPricingAgent.SuggestMarkdownAsync — SuggestedPrice is always re-validated
// server-side (MarkdownPricingAgent.BuildValidatedSuggestion), so a bad model response can only
// be discarded or clamped, never invent a price hike or an absurd markdown.
public record MarkdownSuggestion
{
    public required decimal CurrentPrice { get; init; }
    public required decimal SuggestedPrice { get; init; }
    public required string Explanation { get; init; }
}
