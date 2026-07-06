using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

public class BusinessTypeRepository(EcoMealDbContext context) : IBusinessTypeRepository
{
    public async Task<List<BusinessType>> GetAllAsync()
    {
        return await context.BusinessTypes.ToListAsync();
    }
}