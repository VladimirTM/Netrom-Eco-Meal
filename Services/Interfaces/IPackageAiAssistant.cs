namespace Netrom_Eco_Meal.Services.Interfaces;

// Thin wrapper over IChatClient — kept separate so PackageForm.razor never touches
// Microsoft.Extensions.AI/OllamaSharp types directly, same shape as IStripeGateway sitting
// between CheckoutService and the Stripe SDK.
public interface IPackageAiAssistant
{
    Task<string> DraftDescriptionAsync(string name, string packageTypeName, IReadOnlyList<string> dietaryTags, CancellationToken cancellationToken = default);
}
