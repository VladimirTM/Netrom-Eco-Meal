using NetromEcoMeal.Models;

namespace NetromEcoMeal.Services.Interfaces;

// Thin wrapper over IChatClient's tool-calling — kept separate so BasketPlanner.tsx never
// touches Microsoft.Extensions.AI/OllamaSharp types directly, same shape as IPackageAiAssistant/
// ISearchIntentParser.
public interface IBasketPlannerAgent
{
    Task<BasketPlan> ProposeBasketAsync(int peopleCount, decimal budget, string? dietaryTag, CancellationToken cancellationToken = default);
}
