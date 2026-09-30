using NetromEcoMeal.Entities;
using NetromEcoMeal.Models;

namespace NetromEcoMeal.Services.Interfaces;

// Backs POST /api/webhooks/packages — a POS/inventory system's machine-to-machine entry point,
// authenticated by a business's own API key rather than a signed-in user.
public interface IWebhookIntakeService
{
    // Throws UnauthorizedAccessException for a missing/invalid key or a business that isn't
    // currently live, ArgumentException for an invalid request body.
    public Task<Package> CreatePackageAsync(string? apiKey, WebhookPackageRequest request);
}
