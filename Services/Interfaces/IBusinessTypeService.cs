using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

// Thin pass-through — lookup data has no business rules to enforce.
public interface IBusinessTypeService
{
    public Task<List<BusinessType>> GetAllAsync();
}