using NetromEcoMeal.Entities;
using NetromEcoMeal.Models;

namespace NetromEcoMeal.Services.Interfaces;

// Thin wrapper over IChatClient's tool-calling — kept separate so PackageService/Packages.tsx
// never touch Microsoft.Extensions.AI/OllamaSharp types directly, same shape as IBasketPlannerAgent.
// Takes an already-fetched, already-authorized history rather than querying itself, so it stays a
// pure AI-orchestration layer. Null means no cut is warranted, or the suggestion didn't validate.
public interface IMarkdownPricingAgent
{
    Task<MarkdownSuggestion?> SuggestMarkdownAsync(Package package, List<PackageSellThroughRecord> history, CancellationToken cancellationToken = default);
}
