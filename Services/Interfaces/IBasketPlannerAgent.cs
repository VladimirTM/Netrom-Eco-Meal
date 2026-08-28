using Netrom_Eco_Meal.Models;

namespace Netrom_Eco_Meal.Services.Interfaces;

// Thin wrapper over IChatClient's tool-calling — kept separate so BasketPlanner.razor never
// touches Microsoft.Extensions.AI/OllamaSharp types directly, same shape as IPackageAiAssistant/
// ISearchIntentParser.
public interface IBasketPlannerAgent
{
    Task<BasketPlan> ProposeBasketAsync(int peopleCount, decimal budget, string? dietaryTag, CancellationToken cancellationToken = default);
}
