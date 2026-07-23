using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

// AddAsync/RemoveAsync persist immediately — no staged-then-saved split like elsewhere.
public class FavoriteRepository(EcoMealDbContext context) : IFavoriteRepository
{
    public async Task<HashSet<Guid>> GetFavoriteBusinessIdsAsync(string userId)
    {
        return (await context.Favorites.Where(f => f.UserId == userId).Select(f => f.BusinessId).ToListAsync()).ToHashSet();
    }

    public async Task<bool> IsFavoriteAsync(string userId, Guid businessId)
    {
        return await context.Favorites.AnyAsync(f => f.UserId == userId && f.BusinessId == businessId);
    }

    public async Task AddAsync(string userId, Guid businessId)
    {
        if (await IsFavoriteAsync(userId, businessId))
            return;

        await context.Favorites.AddAsync(new Favorite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BusinessId = businessId,
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();
    }

    public async Task<bool> RemoveAsync(string userId, Guid businessId)
    {
        var favorite = await context.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.BusinessId == businessId);
        if (favorite is null)
            return false;

        context.Favorites.Remove(favorite);
        await context.SaveChangesAsync();
        return true;
    }
}
