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

    public async Task<Business?> GetByIdAsync(Guid id)
    {
        return await businessRepository.GetByIdAsync(id);
    }

    public async Task AddAsync(Business business)
    {
        await businessRepository.AddAsync(business);
        await businessRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Business business)
    {
        var businessFromDb = await businessRepository.GetByIdAsync(business.Id);
        if (businessFromDb is null)
            return;
        UpdateBusiness(business, businessFromDb);
        await businessRepository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Business business)
    {
        await businessRepository.DeleteAsync(business.Id);
        await businessRepository.SaveChangesAsync();
    }

    private static void UpdateBusiness(Business business, Business businessToUpdate)
    {
        businessToUpdate.Name = business.Name;
        businessToUpdate.Description = business.Description;
        businessToUpdate.ImageUrl = business.ImageUrl;
        businessToUpdate.BusinessTypeId = business.BusinessTypeId;
    }
}