using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

public interface IBusinessTypeRepository
{
    public Task<List<BusinessType>> GetAllAsync();
}