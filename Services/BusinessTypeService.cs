using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

// Thin pass-through — lookup data has no business rules to enforce.
public class BusinessTypeService(IBusinessTypeRepository businessTypeRepository) : IBusinessTypeService
{
    public async Task<List<BusinessType>> GetAllAsync()
    {
        return await businessTypeRepository.GetAllAsync();
    }
}