using NetromEcoMeal.Constants;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Models;
using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Services;

public class BrandService(
    IBrandRepository brandRepository,
    IReviewRepository reviewRepository,
    ICurrentUser currentUser,
    IAuditLogService auditLogService) : IBrandService
{
    public async Task<List<Brand>> GetAllAsync()
    {
        return await brandRepository.GetAllAsync();
    }

    public async Task<BrandDetailDto?> GetPublicDetailAsync(Guid id)
    {
        var brand = await brandRepository.GetByIdAsync(id);
        if (brand is null)
            return null;

        var locations = await brandRepository.GetPublicLocationsAsync(id);

        var reviews = await reviewRepository.GetByBusinessIdsAsync(locations.Select(b => b.Id).ToList());
        double? averageRating = reviews.Count > 0 ? reviews.Average(r => r.Rating) : null;

        return new BrandDetailDto { Brand = brand, Locations = locations, AverageRating = averageRating, RatingCount = reviews.Count };
    }

    public async Task AddAsync(Brand brand)
    {
        await currentUser.EnsureAdminAsync("Only an admin can manage brands.");

        var name = brand.Name.Trim();
        // Neither the DB nor any caller enforces uniqueness otherwise — without this, two
        // identically-named brands both show up (indistinguishably) on the brand-assignment form.
        if (await IsNameTakenAsync(name, excludingId: null))
            throw new InvalidOperationException($"A brand named \"{name}\" already exists.");

        brand.Id = Guid.NewGuid();
        brand.Name = name;
        await brandRepository.AddAsync(brand);
        await brandRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.BrandCreated, AuditTargetTypes.Brand, brand.Id.ToString(), brand.Name);
    }

    public async Task UpdateAsync(Brand brand)
    {
        await currentUser.EnsureAdminAsync("Only an admin can manage brands.");

        var existing = await brandRepository.GetByIdAsync(brand.Id);
        if (existing is null)
            return;

        var name = brand.Name.Trim();
        if (await IsNameTakenAsync(name, excludingId: existing.Id))
            throw new InvalidOperationException($"A brand named \"{name}\" already exists.");

        var previousName = existing.Name;
        existing.Name = name;
        await brandRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.BrandUpdated, AuditTargetTypes.Brand, existing.Id.ToString(), existing.Name, $"{previousName} → {existing.Name}");
    }

    // Case-insensitive — two brands named "Golden Boot Bakeries" and "golden boot bakeries" would
    // otherwise both silently exist and both show up as indistinguishable options on the form.
    private async Task<bool> IsNameTakenAsync(string name, Guid? excludingId)
    {
        var all = await brandRepository.GetAllAsync();
        return all.Any(b => b.Id != excludingId && string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task DeleteAsync(Guid id)
    {
        await currentUser.EnsureAdminAsync("Only an admin can manage brands.");

        var brand = await brandRepository.GetByIdAsync(id);
        if (brand is null)
            return;

        if (await brandRepository.IsInUseAsync(id))
            throw new InvalidOperationException($"\"{brand.Name}\" still has at least one business assigned to it — reassign or remove those first.");

        await brandRepository.DeleteAsync(brand);
        await brandRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.BrandDeleted, AuditTargetTypes.Brand, id.ToString(), brand.Name);
    }

    public async Task<HashSet<Guid>> GetMyFavoriteBrandIdsAsync()
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null || !await currentUser.IsInRoleAsync(AppRoles.Customer))
            return [];

        return await brandRepository.GetFavoriteBrandIdsAsync(userId);
    }

    public async Task<bool> ToggleFavoriteAsync(Guid brandId)
    {
        if (!await currentUser.IsInRoleAsync(AppRoles.Customer))
            throw new UnauthorizedAccessException("Only customers can favorite a brand.");

        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to favorite a brand.");

        var removed = await brandRepository.RemoveFavoriteAsync(userId, brandId);
        if (removed)
            return false;

        await brandRepository.AddFavoriteAsync(userId, brandId);
        return true;
    }
}
