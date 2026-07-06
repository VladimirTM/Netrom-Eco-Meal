using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

public interface IBusinessRepository
{
    public Task<List<Business>> GetAllAsync();
    public Task<Business?> GetByIdAsync(Guid id);
    public Task AddAsync(Business business);
    public Task DeleteAsync(Guid id);
    public Task SaveChangesAsync();
}