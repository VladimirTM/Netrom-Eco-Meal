namespace Netrom_Eco_Meal.Repositories.Interfaces;

// AddAsync only stages the change — call SaveChangesAsync to persist.
public interface IFavoriteRepository
{
    public Task<HashSet<Guid>> GetFavoriteBusinessIdsAsync(string userId);
    public Task<bool> IsFavoriteAsync(string userId, Guid businessId);
    public Task AddAsync(string userId, Guid businessId);
    public Task<bool> RemoveAsync(string userId, Guid businessId);
}
