using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

// AddAsync only stages the change — call SaveChangesAsync to persist.
public interface IFavoriteRepository
{
    public Task<HashSet<Guid>> GetFavoriteBusinessIdsAsync(string userId);
    public Task<bool> IsFavoriteAsync(string userId, Guid businessId);
    public Task AddAsync(string userId, Guid businessId);
    public Task<bool> RemoveAsync(string userId, Guid businessId);
    // Feeds back-in-stock notifications — everyone who's favorited this business.
    public Task<List<ApplicationUser>> GetFavoritingUsersAsync(Guid businessId);
}
