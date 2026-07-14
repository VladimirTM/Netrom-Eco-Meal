using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public interface IOrderRepository
{
    public Task<List<Order>> GetAllAsync();
    public Task<List<Order>> GetByUserIdAsync(string userId);
    public Task<List<Order>> GetByBusinessIdAsync(Guid businessId);
    public Task<Order?> GetByIdAsync(Guid id);
    public Task AddAsync(Order order);
    public Task DeleteAsync(Guid id);
    public Task SaveChangesAsync();
}
