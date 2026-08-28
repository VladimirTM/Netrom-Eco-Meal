using System.Text.Json;
using Microsoft.Extensions.AI;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services.AI;

// Highest-value, highest-risk AI feature so far — a bad suggestion costs a manager real revenue,
// so it's never applied automatically, only shown as a dismissable suggestion on /packages. Same
// two-turn shape as BasketPlannerAgent: turn 1 gives the model a get_sell_through_history tool —
// a closure over history the caller (PackageService.GetMarkdownSuggestionAsync) already fetched
// and authorized, not a fresh DB query; turn 2, tools off, asks for a schema-constrained
// suggestion. BuildValidatedSuggestion re-checks the price afterward — it must be a real cut
// below the package's current price and never below MarkdownSettings.MinPriceFraction of it, so
// a bad response can only be discarded or clamped, never invent a price hike.
public class MarkdownPricingAgent(IChatClient? chatClient = null) : IMarkdownPricingAgent
{
    private readonly IChatClient? _toolClient = chatClient is null ? null : chatClient.AsBuilder().UseFunctionInvocation().Build();

    public async Task<MarkdownSuggestion?> SuggestMarkdownAsync(Package package, List<PackageSellThroughRecord> history, CancellationToken cancellationToken = default)
    {
        if (chatClient is null || _toolClient is null)
            throw new InvalidOperationException("AI features aren't available yet — set Ollama:BaseUrl to enable them.");

        List<PackageSellThroughRecord> GetSellThroughHistory() => history;

        var historyTool = AIFunctionFactory.Create(
            GetSellThroughHistory,
            "get_sell_through_history",
            $"""
            Returns this business's real, closed past packages from the last {MarkdownSettings.LookbackDays}
            days — each with its price, how many portions sold, how many were offered, and its sell-through
            rate — so you can see which prices actually sold well for similar packages. Only ever returns
            real data, never invented. Call this before suggesting anything.
            """);

        var minutesLeft = Math.Max(0, (int)Math.Round((package.PickupEnd - DateTime.UtcNow).TotalMinutes));
        var messages = new List<ChatMessage> { new(ChatRole.User, BuildSearchPrompt(package, minutesLeft)) };

        ChatResponse toolRoundResponse;
        try
        {
            toolRoundResponse = await _toolClient.GetResponseAsync(messages, new ChatOptions
            {
                Temperature = 0,
                Tools = [historyTool],
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("The AI pricing assistant couldn't look up sell-through history right now — try again in a moment.", ex);
        }

        messages.AddRange(toolRoundResponse.Messages);
        messages.Add(new ChatMessage(ChatRole.User, FinalSuggestionPrompt));

        RawMarkdownSuggestion? raw;
        try
        {
            var response = await chatClient.GetResponseAsync(messages, new ChatOptions
            {
                Temperature = 0,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<RawMarkdownSuggestion>(schemaName: "MarkdownSuggestion"),
            }, cancellationToken);
            // Same camelCase-schema-property convention ForJsonSchema<T> relies on as
            // SearchIntentParser/BasketPlannerAgent — a plain Deserialize<T>() would otherwise
            // miss every property against these PascalCase C# names.
            raw = JsonSerializer.Deserialize<RawMarkdownSuggestion>(response.Text, AIJsonUtilities.DefaultOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("The AI pricing assistant couldn't suggest a price right now — try again in a moment.", ex);
        }

        if (raw is null)
            throw new InvalidOperationException("The AI pricing assistant couldn't suggest a price right now — try again in a moment.");

        return BuildValidatedSuggestion(raw, package.Price);
    }

    private static string BuildSearchPrompt(Package package, int minutesLeft) => $"""
        You are a markdown-pricing assistant for a surplus food marketplace fighting food waste.
        This package is still unsold with its pickup window closing soon:

        Name: {package.Name}
        Current price: {package.Price:0.##} RON
        Portions still unsold: {package.Quantity}
        Minutes until pickup closes: {minutesLeft}

        Use the get_sell_through_history tool to see how similar past packages at this business
        actually sold at different prices before deciding anything — never invent a number. Once
        you've looked, reply with a short confirmation that you're ready to suggest a price —
        don't give the number yet.
        """;

    private const string FinalSuggestionPrompt = """
        Suggest a price now. Respond only with JSON matching the schema — no explanation outside
        the explanation field, no markdown. If a lower price is genuinely likely to sell more
        portions before the window closes based on the real history you saw, suggestedPrice should
        be lower than the current price, and explanation should say why in one short sentence,
        referencing the real history if it supports the cut. If the history doesn't support a cut,
        set suggestedPrice equal to the current price and explain that no markdown is needed.
        """;

    // Re-validates the model's own numbers instead of trusting them — a suggestion that isn't a
    // real cut is dropped (null), one below the sane floor is clamped up to it.
    private static MarkdownSuggestion? BuildValidatedSuggestion(RawMarkdownSuggestion raw, decimal currentPrice)
    {
        if (raw.SuggestedPrice <= 0 || raw.SuggestedPrice >= currentPrice)
            return null;

        var floor = Math.Round(currentPrice * MarkdownSettings.MinPriceFraction, 2);
        var suggestedPrice = Math.Max(floor, Math.Round(raw.SuggestedPrice, 2));
        if (suggestedPrice >= currentPrice)
            return null;

        var explanation = string.IsNullOrWhiteSpace(raw.Explanation)
            ? "A lower price is more likely to sell before the pickup window closes."
            : raw.Explanation.Trim();

        return new MarkdownSuggestion
        {
            CurrentPrice = currentPrice,
            SuggestedPrice = suggestedPrice,
            Explanation = explanation,
        };
    }

    // Deserialization target for the final turn's raw JSON — kept separate from MarkdownSuggestion
    // so an unvalidated SuggestedPrice can never leak into the public type without going through
    // BuildValidatedSuggestion first.
    private record RawMarkdownSuggestion
    {
        public decimal SuggestedPrice { get; init; }
        public string? Explanation { get; init; }
    }
}
