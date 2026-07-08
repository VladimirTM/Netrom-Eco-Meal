using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

public interface IPackageService
{
    public Task<List<Package>> GetAllAsync();
    public Task<Package?> GetByIdAsync(Guid id);
    public Task AddAsync(Package package);
    public Task UpdateAsync(Package package);
    public Task DeleteAsync(Package package);
}