using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class PackageService(IPackageRepository packageRepository) : IPackageService
{
    public async Task<List<Package>> GetAllAsync()
    {
        return await packageRepository.GetAllAsync();
    }

    public async Task<Package?> GetByIdAsync(Guid id)
    {
        return await packageRepository.GetByIdAsync(id);
    }

    public async Task AddAsync(Package package)
    {
        await packageRepository.AddAsync(package);
        await packageRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Package package)
    {
        var packageFromDb = await packageRepository.GetByIdAsync(package.Id);
        if (packageFromDb is null)
            return;
        UpdatePackage(package, packageFromDb);
        await packageRepository.SaveChangesAsync();
    }
    
    public async Task DeleteAsync(Package package)
    {
        await packageRepository.DeleteAsync(package.Id);
        await packageRepository.SaveChangesAsync();
    }

    private static void UpdatePackage(Package package, Package packageToUpdate)
    {
        packageToUpdate.Name = package.Name;
        packageToUpdate.Description = package.Description;
        packageToUpdate.BusinessId = package.BusinessId;
        packageToUpdate.PackageTypeId = package.PackageTypeId;
        packageToUpdate.Price = package.Price;
        packageToUpdate.Quantity = package.Quantity;
        packageToUpdate.PickupStart = package.PickupStart;
        packageToUpdate.PickupEnd = package.PickupEnd;
        packageToUpdate.ImageUrl = package.ImageUrl;
    }
}