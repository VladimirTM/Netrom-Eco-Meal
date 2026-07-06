using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

public interface IBusinessTypeService
{
    public Task<List<BusinessType>> GetAllAsync();
}