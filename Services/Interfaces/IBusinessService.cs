using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

public interface IBusinessService
{
    public Task<List<Business>> GetAllAsync();
    public Task AddAsync(Business business);
}