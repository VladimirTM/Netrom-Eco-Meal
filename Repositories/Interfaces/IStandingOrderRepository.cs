using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public interface IStandingOrderRepository
{
    public Task<List<StandingOrder>> GetByUserIdAsync(string userId);
    public Task<StandingOrder?> GetByIdAsync(Guid id);
    // Active standing orders at a business, optionally narrowed to those whose own PackageTypeId
    // either matches the given one or is unset — feeds the post-generation matching sweep.
    public Task<List<StandingOrder>> GetActiveByBusinessIdAsync(Guid businessId);
    public Task AddAsync(StandingOrder standingOrder);
    public Task DeleteAsync(Guid id);
    public Task SaveChangesAsync();
}
