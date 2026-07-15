using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

// AddAsync only stages the change — call SaveChangesAsync to persist.
public interface IReviewRepository
{
    public Task<List<Review>> GetAllAsync();
    public Task<List<Review>> GetByBusinessIdAsync(Guid businessId);
    public Task<List<Review>> GetByBusinessIdsAsync(IReadOnlyCollection<Guid> businessIds);
    public Task<Review?> GetByUserAndBusinessAsync(string userId, Guid businessId);
    public Task AddAsync(Review review);
    public Task SaveChangesAsync();
}
