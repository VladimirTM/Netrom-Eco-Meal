using System.Text.Json;
using Microsoft.Extensions.AI;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services.AI;

// Same null-chatClient-degrades-gracefully constructor shape as PackageAiAssistant.
public class SearchIntentParser(IChatClient? chatClient = null) : ISearchIntentParser
{
    private static readonly ChatOptions Options = new()
    {
        // Deterministic extraction, not creative writing.
        Temperature = 0,
        ResponseFormat = ChatResponseFormat.ForJsonSchema<RawSearchIntent>(schemaName: "SearchIntent"),
    };

    public async Task<SearchIntent> ParseAsync(string utterance, SearchIntent? previousIntent = null, CancellationToken cancellationToken = default)
    {
        if (chatClient is null)
            throw new InvalidOperationException("AI features aren't available yet — set Ollama:BaseUrl to enable them.");

        var prompt = BuildPrompt(utterance, previousIntent);

        RawSearchIntent? raw;
        try
        {
            var response = await chatClient.GetResponseAsync(prompt, Options, cancellationToken);
            // Same options ForJsonSchema<T> above used to name the schema's properties
            // (camelCase, case-insensitive) — a plain Deserialize<T>() would otherwise miss
            // every property against these PascalCase C# names.
            raw = JsonSerializer.Deserialize<RawSearchIntent>(response.Text, AIJsonUtilities.DefaultOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("The AI search assistant couldn't understand that right now — try again in a moment.", ex);
        }

        if (raw is null)
            throw new InvalidOperationException("The AI search assistant couldn't understand that right now — try again in a moment.");

        return Validate(raw);
    }

    private static string BuildPrompt(string utterance, SearchIntent? previousIntent)
    {
        var knownTags = string.Join(", ", DietaryTags.All);
        var refinementContext = previousIntent is null
            ? ""
            : $"""

                The shopper already has these filters applied from an earlier request in this
                conversation: {JsonSerializer.Serialize(previousIntent)}
                Treat the new request as a refinement of those — e.g. "cheaper" should lower
                maxPrice below its current value, "gluten-free only" should replace dietaryTag,
                and any field the new request doesn't mention should keep its current value
                instead of being reset.
                """;

        return $"""
            You extract search filters for a surplus-food marketplace from a shopper's
            natural-language request. Respond only with a JSON object matching the schema —
            no explanation, no markdown.

            Fields:
            - keywords: any leftover free-text search terms (cuisine, food/kitchen type, etc.)
              not covered by the fields below, or null if nothing is left.
            - dietaryTag: at most one tag from this exact list that best matches the request, or
              null if none is mentioned: {knownTags}
            - maxPrice: a price ceiling in RON if one is mentioned (e.g. "under 30 lei" -> 30,
              "cheaper than 20" -> 20), or null.
            - closingSoon: true only if the shopper wants results sorted by soonest pickup/closing
              time (e.g. "closing soon", "about to expire").
            - nearMe: true only if the shopper wants results sorted by distance from their current
              location (e.g. "near me", "nearby").
            {refinementContext}
            Shopper's request: "{utterance}"
            """;
    }

    // The LLM's raw output is untrusted — dietaryTag in particular has to be checked against the
    // real vocabulary, since nothing stops the model from inventing a tag that isn't one of
    // Constants.DietaryTags.All. Never trust free-text output to drive a filter directly.
    private static SearchIntent Validate(RawSearchIntent raw)
    {
        var dietaryTag = DietaryTags.All.FirstOrDefault(t => string.Equals(t, raw.DietaryTag?.Trim(), StringComparison.OrdinalIgnoreCase));
        var keywords = string.IsNullOrWhiteSpace(raw.Keywords) ? null : raw.Keywords.Trim();
        var maxPrice = raw.MaxPrice is > 0 ? raw.MaxPrice : null;

        return new SearchIntent
        {
            Keywords = keywords,
            DietaryTag = dietaryTag,
            MaxPrice = maxPrice,
            ClosingSoon = raw.ClosingSoon,
            NearMe = raw.NearMe,
        };
    }

    // Deserialization target for the model's raw JSON — kept separate from SearchIntent so an
    // unvalidated DietaryTag/MaxPrice can never leak into the public type without going through
    // Validate() first.
    private record RawSearchIntent
    {
        public string? Keywords { get; init; }
        public string? DietaryTag { get; init; }
        public decimal? MaxPrice { get; init; }
        public bool ClosingSoon { get; init; }
        public bool NearMe { get; init; }
    }
}
