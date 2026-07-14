using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

// Read-only lookup over the seeded BusinessType list.
public interface IBusinessTypeRepository
{
    public Task<List<BusinessType>> GetAllAsync();
}