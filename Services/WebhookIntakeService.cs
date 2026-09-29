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
        // Same upper bounds PackageForm.razor's PackageFormModel enforces via [Range] — this is
        // the app's other entry point for creating a Package, and used to have no ceiling at
        // all (a POS system could post a 999,999 quantity/price no manager could ever type in).
        if (request.Price is <= 0 or > PackageLimits.MaxPrice)
            throw new ArgumentException($"Price must be greater than zero and at most {PackageLimits.MaxPrice:0}.");
        if (request.Quantity < 0)
            throw new ArgumentException("Quantity can't be negative.");
        if (request.Quantity > PackageLimits.MaxQuantity)
            throw new ArgumentException($"Quantity can't be more than {PackageLimits.MaxQuantity}.");
        if (request.WeightKg is <= 0 or > PackageLimits.MaxWeightKg)
            throw new ArgumentException($"WeightKg must be greater than zero and at most {PackageLimits.MaxWeightKg:0}.");
        if (request.PickupEnd <= request.PickupStart)
            throw new ArgumentException("PickupEnd must be after PickupStart.");
        if (await packageTypeRepository.GetByIdAsync(request.PackageTypeId) is null)
            throw new ArgumentException("Unknown PackageTypeId — see GET /api/package-types for valid ids.");

        // Every other field is validated against the app's own closed vocabularies/limits —
        // dietary tags weren't, so a POS system could post anything (including markup) and it
        // would render straight onto the public kitchen page as if it were a real diet/allergen
        // tag. Reject the whole request the same way an unknown PackageTypeId does, rather than
        // silently dropping the bad ones, and normalize to the canonical casing so it matches
        // every other source of these tags (the manager form, the dietary filter).
        var dietaryTags = new List<string>();
        foreach (var tag in request.DietaryTags ?? [])
        {
            var canonical = DietaryTags.All.FirstOrDefault(t => string.Equals(t, tag?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (canonical is null)
                throw new ArgumentException($"Unknown dietary tag \"{tag}\" — must be one of: {string.Join(", ", DietaryTags.All)}.");
            if (!dietaryTags.Contains(canonical))
                dietaryTags.Add(canonical);
        }

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
            DietaryTags = dietaryTags,
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
