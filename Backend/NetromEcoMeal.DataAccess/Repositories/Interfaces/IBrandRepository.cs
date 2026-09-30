using NetromEcoMeal.Entities;

namespace NetromEcoMeal.Repositories.Interfaces;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist. Favorite methods
// mirror IFavoriteRepository, just keyed on BrandId instead of BusinessId.
public interface IBrandRepository
{
    public Task<List<Brand>> GetAllAsync();
    public Task<Brand?> GetByIdAsync(Guid id);
    // Same "in use" delete guard as IBusinessTypeRepository.IsInUseAsync, over Business.BrandId.
    public Task<bool> IsInUseAsync(Guid id);
    public Task AddAsync(Brand brand);
    public Task DeleteAsync(Brand brand);
    public Task SaveChangesAsync();

    // Public-only locations for a brand's storefront page, ordered by name.
    public Task<List<Business>> GetPublicLocationsAsync(Guid brandId);

    public Task<HashSet<Guid>> GetFavoriteBrandIdsAsync(string userId);
    public Task<bool> IsFavoriteAsync(string userId, Guid brandId);
    public Task AddFavoriteAsync(string userId, Guid brandId);
    public Task<bool> RemoveFavoriteAsync(string userId, Guid brandId);
}
