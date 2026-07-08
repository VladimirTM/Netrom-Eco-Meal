using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

public interface IBusinessService
{
    public Task<List<Business>> GetAllAsync();
    public Task<Business?> GetByIdAsync(Guid id);
    public Task AddAsync(Business business);
    public Task UpdateAsync(Business business);
    public Task DeleteAsync(Business business);
}