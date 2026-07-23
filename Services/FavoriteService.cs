using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class FavoriteService(IFavoriteRepository favoriteRepository, CurrentUserAccessor currentUser) : IFavoriteService
{
    public async Task<HashSet<Guid>> GetMyFavoriteBusinessIdsAsync()
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null || !await currentUser.IsInRoleAsync(AppRoles.Customer))
            return [];

        return await favoriteRepository.GetFavoriteBusinessIdsAsync(userId);
    }

    public async Task<bool> ToggleFavoriteAsync(Guid businessId)
    {
        if (!await currentUser.IsInRoleAsync(AppRoles.Customer))
            throw new UnauthorizedAccessException("Only customers can favorite a business.");

        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to favorite a business.");

        var removed = await favoriteRepository.RemoveAsync(userId, businessId);
        if (removed)
            return false;

        await favoriteRepository.AddAsync(userId, businessId);
        return true;
    }
}
