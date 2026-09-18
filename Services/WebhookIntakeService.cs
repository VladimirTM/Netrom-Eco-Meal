using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

// The one entry point into this app with no signed-in user at all — see IWebhookIntakeService.
public class WebhookIntakeService(
    IBusinessRepository businessRepository,
    IPackageTypeRepository packageTypeRepository,
    IPackageService packageService) : IWebhookIntakeService
{
    public async Task<Package> CreatePackageAsync(string? apiKey, WebhookPackageRequest request)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new UnauthorizedAccessException("Missing X-Api-Key header.");

        var business = await businessRepository.GetByApiKeyHashAsync(ApiKeyHasher.Hash(apiKey));
        if (business is null)
            throw new UnauthorizedAccessException("Invalid API key.");

        // Same visibility rule the public storefront enforces — a hidden or not-yet-approved
        // business shouldn't be able to inject packages that would never actually be seen.
        if (business.Status != BusinessStatuses.Approved || business.IsHidden)
            throw new UnauthorizedAccessException("This business isn't currently live on Eco Meal.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.");
        if (request.Price <= 0)
            throw new ArgumentException("Price must be greater than zero.");
        if (request.Quantity < 0)
            throw new ArgumentException("Quantity can't be negative.");
        if (request.WeightKg <= 0)
            throw new ArgumentException("WeightKg must be greater than zero.");
        if (request.PickupEnd <= request.PickupStart)
            throw new ArgumentException("PickupEnd must be after PickupStart.");
        if (await packageTypeRepository.GetByIdAsync(request.PackageTypeId) is null)
            throw new ArgumentException("Unknown PackageTypeId — see GET /api/package-types for valid ids.");

        var package = new Package
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            PackageTypeId = request.PackageTypeId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? "",
            Price = request.Price,
            Quantity = request.Quantity,
            WeightKg = request.WeightKg,
            DietaryTags = request.DietaryTags ?? [],
            PickupStart = DateTime.SpecifyKind(request.PickupStart, DateTimeKind.Utc),
            PickupEnd = DateTime.SpecifyKind(request.PickupEnd, DateTimeKind.Utc),
            ImageUrl = request.ImageUrl,
        };

        await packageService.AddFromWebhookAsync(package);

        business.WebhookApiKeyLastUsedAt = DateTime.UtcNow;
        await businessRepository.SaveChangesAsync();

        return package;
    }
}
