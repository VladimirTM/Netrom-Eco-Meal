using NetromEcoMeal.Entities;
using NetromEcoMeal.Models;

namespace NetromEcoMeal.Services.Interfaces;

// Create/update/delete are admin-only, same as IBusinessTypeService — a chain grouping is
// platform taxonomy, not something an individual business manager self-serves.
public interface IBrandService
{
    public Task<List<Brand>> GetAllAsync();
    // Public storefront view — null if the brand doesn't exist. Locations are filtered to
    // Approved/not-hidden, and the rating is aggregated across all of them.
    public Task<BrandDetailDto?> GetPublicDetailAsync(Guid id);
    public Task AddAsync(Brand brand);
    public Task UpdateAsync(Brand brand);
    public Task DeleteAsync(Guid id);

    // Customer-only, always scoped to the signed-in user — same shape as IFavoriteService.
    public Task<HashSet<Guid>> GetMyFavoriteBrandIdsAsync();
    public Task<bool> ToggleFavoriteAsync(Guid brandId);
}
