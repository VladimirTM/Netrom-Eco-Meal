namespace Netrom_Eco_Meal.Services.Interfaces;

// Customer-only, always scoped to the signed-in user — there's no "favorite on behalf of" path.
public interface IFavoriteService
{
    public Task<HashSet<Guid>> GetMyFavoriteBusinessIdsAsync();
    // Returns the new favorited state (true = now favorited).
    public Task<bool> ToggleFavoriteAsync(Guid businessId);
}
