using System.Text.Json;
using Microsoft.Extensions.AI;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services.AI;

// First AI feature needing Ollama's tool-calling, not just plain text or schema-only output.
// Split into two chat turns since a free local model isn't reliably good at combining
// tool-calling and strict JSON-schema output in one turn: turn 1 lets the model call the search
// tool over real IPackageService data; turn 2, tools off, asks for the final basket as
// schema-constrained JSON referencing only packages the tool actually returned. Every item is
// still re-validated afterward (BuildValidatedPlan) — a bad response can only narrow/drop items,
// never invent one. Same null-chatClient-degrades-gracefully constructor shape as
// PackageAiAssistant/SearchIntentParser.
public class BasketPlannerAgent(IPackageService packageService, IChatClient? chatClient = null) : IBasketPlannerAgent
{
    // Only the first, free-form turn gets the search tool — the second, JSON-schema turn below
    // deliberately talks to the plain chatClient instead, with no tools attached.
    private readonly IChatClient? _toolClient = chatClient is null ? null : chatClient.AsBuilder().UseFunctionInvocation().Build();

    public async Task<BasketPlan> ProposeBasketAsync(int peopleCount, decimal budget, string? dietaryTag, CancellationToken cancellationToken = default)
    {
        if (chatClient is null || _toolClient is null)
            throw new InvalidOperationException("AI features aren't available yet — set Ollama:BaseUrl to enable them.");

        if (peopleCount <= 0 || budget <= 0)
            throw new InvalidOperationException("Enter a positive number of people and budget.");

        // Populated as a side effect of the search tool below — the only source BuildValidatedPlan
        // can ever resolve a packageId against, so a hallucinated one just won't match.
        var candidatePool = new Dictionary<Guid, Package>();

        async Task<List<PackageSearchResult>> SearchLivePackagesAsync(string? tag = null)
        {
            // Never trust a tool argument either — same re-validation SearchIntentParser applies
            // to a free-text dietaryTag extraction.
            var validTag = DietaryTags.All.FirstOrDefault(t => string.Equals(t, tag?.Trim(), StringComparison.OrdinalIgnoreCase));
            var packages = await packageService.GetLiveCandidatesAsync(validTag);
            foreach (var package in packages)
                candidatePool[package.Id] = package;

            return packages.Select(p => new PackageSearchResult(
                p.Id,
                p.Name,
                p.Business.Name,
                p.Price,
                p.Quantity,
                [..p.DietaryTags],
                Math.Max(0, (int)Math.Round((p.PickupEnd - DateTime.UtcNow).TotalMinutes)))).ToList();
        }

        var searchTool = AIFunctionFactory.Create(
            SearchLivePackagesAsync,
            "search_live_packages",
            $"""
            Search for real, in-stock, live surplus food packages a customer could order right
            now. Only ever returns real data — never invent a package, its price, or its kitchen.
            Optionally filter by a dietary tag: one of {string.Join(", ", DietaryTags.All)}
            (case-insensitive; omit for no filter). Call this as many times as you need, e.g. once
            per dietary tag or kitchen you want to compare, before proposing a basket.
            """);

        var messages = new List<ChatMessage> { new(ChatRole.User, BuildSearchPrompt(peopleCount, budget, dietaryTag)) };

        ChatResponse toolRoundResponse;
        try
        {
            toolRoundResponse = await _toolClient.GetResponseAsync(messages, new ChatOptions
            {
                Temperature = 0,
                Tools = [searchTool],
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("The AI basket planner couldn't search for packages right now — try again in a moment.", ex);
        }

        messages.AddRange(toolRoundResponse.Messages);
        messages.Add(new ChatMessage(ChatRole.User, FinalPlanPrompt));

        RawBasketPlan? raw;
        try
        {
            var response = await chatClient.GetResponseAsync(messages, new ChatOptions
            {
                Temperature = 0,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<RawBasketPlan>(schemaName: "BasketPlan"),
            }, cancellationToken);
            // Same camelCase-schema-property convention ForJsonSchema<T> relies on as
            // SearchIntentParser — a plain Deserialize<T>() would otherwise miss every property
            // against these PascalCase C# names.
            raw = JsonSerializer.Deserialize<RawBasketPlan>(response.Text, AIJsonUtilities.DefaultOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("The AI basket planner couldn't propose a basket right now — try again in a moment.", ex);
        }

        if (raw is null)
            throw new InvalidOperationException("The AI basket planner couldn't propose a basket right now — try again in a moment.");

        return BuildValidatedPlan(raw, candidatePool, budget);
    }

    private static string BuildSearchPrompt(int peopleCount, decimal budget, string? dietaryTag)
    {
        var dietaryLine = string.IsNullOrWhiteSpace(dietaryTag) ? "" : $" The customer wants {dietaryTag} options only.";

        return $"""
            You are a rescue-basket planner for a surplus food marketplace that fights food waste.
            A customer wants to feed {peopleCount} {(peopleCount == 1 ? "person" : "people")} for
            at most {budget:0.##} RON total.{dietaryLine}

            Each package's quantity represents individual portions, so aim for roughly
            {peopleCount} total portions across the basket if the budget allows it — but never
            exceed the {budget:0.##} RON budget.

            IMPORTANT: an order can only include packages from ONE kitchen (business) at a time.
            If your best options span multiple kitchens, pick the single kitchen that gives the
            best combined value within budget instead of mixing kitchens, and only search that one
            further.

            Use the search_live_packages tool to find real packages before deciding anything —
            never invent a package, a price, or a kitchen. Call it as many times as you need. Once
            you're done searching, reply with a short confirmation that you're ready to propose a
            basket — don't list the basket yet.
            """;
    }

    private const string FinalPlanPrompt = """
        Propose the final basket now. Respond only with JSON matching the schema — no explanation,
        no markdown. Only use packageId values that search_live_packages actually returned above;
        never invent one. items should be empty if nothing found fits. explanation is one short
        paragraph summarizing the basket, and, if you skipped kitchens or items to keep to a single
        kitchen or the budget, briefly why.
        """;

    // Re-validates every field against candidatePool instead of trusting the JSON directly — a
    // hallucinated packageId is dropped, quantities are clamped to real stock, and prices/totals
    // are recomputed from the real Package. The single-kitchen rule is enforced again here too,
    // in case the model didn't fully comply with the prompt.
    private static BasketPlan BuildValidatedPlan(RawBasketPlan raw, Dictionary<Guid, Package> candidatePool, decimal budget)
    {
        var picked = new List<BasketPlanItem>();
        foreach (var rawItem in raw.Items)
        {
            if (!Guid.TryParse(rawItem.PackageId, out var id) || !candidatePool.TryGetValue(id, out var package))
                continue;

            picked.Add(new BasketPlanItem
            {
                Package = package,
                Quantity = Math.Clamp(rawItem.Quantity, 1, package.Quantity),
                Reason = string.IsNullOrWhiteSpace(rawItem.Reason) ? "Fits your basket." : rawItem.Reason.Trim(),
            });
        }

        var spansMultipleKitchens = picked.Select(i => i.Package.BusinessId).Distinct().Count() > 1;
        if (spansMultipleKitchens)
        {
            var bestBusinessId = picked
                .GroupBy(i => i.Package.BusinessId)
                .OrderByDescending(g => g.Sum(i => i.LineTotal))
                .First().Key;
            picked = picked.Where(i => i.Package.BusinessId == bestBusinessId).ToList();
        }

        // Drop the lowest-priority (last-listed) items until the total fits the budget, in case
        // the model's own arithmetic was off.
        while (picked.Count > 0 && picked.Sum(i => i.LineTotal) > budget)
            picked.RemoveAt(picked.Count - 1);

        // Once the trim above empties the basket, the model's own explanation is stale — it
        // describes items that no longer appear — so it's replaced rather than shown.
        string explanation;
        if (picked.Count == 0)
        {
            explanation = "No live packages matched that budget and dietary combination right now — try a higher budget or a different dietary filter.";
        }
        else
        {
            explanation = string.IsNullOrWhiteSpace(raw.Explanation) ? "Here's a basket that fits." : raw.Explanation.Trim();
            if (spansMultipleKitchens)
                explanation += " (Only one kitchen's picks are shown — an order can only combine packages from a single kitchen.)";
        }

        return new BasketPlan
        {
            Items = picked,
            TotalPrice = picked.Sum(i => i.LineTotal),
            Explanation = explanation,
        };
    }

    // The search tool's return shape — deliberately small/flat so the model sees just what it
    // needs to reason about a basket, not the full Package entity graph.
    private record PackageSearchResult(
        Guid PackageId,
        string PackageName,
        string BusinessName,
        decimal Price,
        int QuantityAvailable,
        List<string> DietaryTags,
        int MinutesUntilClose);

    // Deserialization target for the final turn's raw JSON — kept separate from BasketPlan so an
    // unvalidated PackageId/Quantity can never leak into the public type without going through
    // BuildValidatedPlan first.
    private record RawBasketPlan
    {
        public List<RawBasketItem> Items { get; init; } = [];
        public string? Explanation { get; init; }
    }

    private record RawBasketItem
    {
        public string PackageId { get; init; } = "";
        public int Quantity { get; init; }
        public string? Reason { get; init; }
    }
}
