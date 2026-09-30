using Microsoft.EntityFrameworkCore;
using NetromEcoMeal.Database;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories.Interfaces;

namespace NetromEcoMeal.Repositories;

// AddAsync only stages the change — call SaveChangesAsync to persist.
public class ReviewRepository(EcoMealDbContext context) : IReviewRepository
{
    public async Task<List<Review>> GetAllAsync()
    {
        return await context.Reviews.ToListAsync();
    }

    public async Task<List<Review>> GetByBusinessIdAsync(Guid businessId)
    {
        return await context.Reviews
            .Include(r => r.User)
            .Include(r => r.Package)
            .Where(r => r.BusinessId == businessId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Review>> GetByBusinessIdsAsync(IReadOnlyCollection<Guid> businessIds)
    {
        return await context.Reviews
            .Where(r => businessIds.Contains(r.BusinessId))
            .ToListAsync();
    }

    public async Task<Review?> GetByUserAndBusinessAsync(string userId, Guid businessId)
    {
        return await context.Reviews.FirstOrDefaultAsync(r => r.UserId == userId && r.BusinessId == businessId);
    }

    public async Task AddAsync(Review review)
    {
        await context.Reviews.AddAsync(review);
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}
