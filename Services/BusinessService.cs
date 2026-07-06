using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class BusinessService(IBusinessRepository businessRepository) : IBusinessService
{
    public async Task<List<Business>> GetAllAsync()
    {
        return await businessRepository.GetAllAsync();
    }

    public async Task AddAsync(Business business)
    {
        await businessRepository.AddAsync(business);
        await businessRepository.SaveChangesAsync();
    }
}