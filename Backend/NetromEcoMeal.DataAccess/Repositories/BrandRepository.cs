using Microsoft.EntityFrameworkCore;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Database;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories.Interfaces;

namespace NetromEcoMeal.Repositories;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public class BrandRepository(EcoMealDbContext context) : IBrandRepository
{
    public async Task<List<Brand>> GetAllAsync()
    {
        return await context.Brands.Include(b => b.Businesses).OrderBy(b => b.Name).ToListAsync();
    }

    public async Task<Brand?> GetByIdAsync(Guid id)
    {
        return await context.Brands.Include(b => b.Businesses).ThenInclude(b => b.BusinessType).FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<bool> IsInUseAsync(Guid id)
    {
        return await context.Businesses.AnyAsync(b => b.BrandId == id);
    }

    public async Task AddAsync(Brand brand)
    {
        await context.Brands.AddAsync(brand);
    }

    public async Task DeleteAsync(Brand brand)
    {
        context.Brands.Remove(brand);
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }

    public async Task<List<Business>> GetPublicLocationsAsync(Guid brandId)
    {
        return await context.Businesses
            .Include(b => b.BusinessType).Include(b => b.Hours).Include(b => b.Closures)
            .AsSplitQuery()
            .Where(b => b.BrandId == brandId && b.Status == BusinessStatuses.Approved && !b.IsHidden)
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<HashSet<Guid>> GetFavoriteBrandIdsAsync(string userId)
    {
        return (await context.BrandFavorites.Where(f => f.UserId == userId).Select(f => f.BrandId).ToListAsync()).ToHashSet();
    }

    public async Task<bool> IsFavoriteAsync(string userId, Guid brandId)
    {
        return await context.BrandFavorites.AnyAsync(f => f.UserId == userId && f.BrandId == brandId);
    }

    public async Task AddFavoriteAsync(string userId, Guid brandId)
    {
        if (await IsFavoriteAsync(userId, brandId))
            return;

        await context.BrandFavorites.AddAsync(new BrandFavorite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BrandId = brandId,
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();
    }

    public async Task<bool> RemoveFavoriteAsync(string userId, Guid brandId)
    {
        var favorite = await context.BrandFavorites.FirstOrDefaultAsync(f => f.UserId == userId && f.BrandId == brandId);
        if (favorite is null)
            return false;

        context.BrandFavorites.Remove(favorite);
        await context.SaveChangesAsync();
        return true;
    }
}
