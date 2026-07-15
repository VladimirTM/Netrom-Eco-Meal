using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

// Write methods are restricted to admins and the package's own business manager.
public class PackageService(
    IPackageRepository packageRepository,
    IBusinessService businessService,
    CurrentUserAccessor currentUser) : IPackageService
{
    public async Task<List<Package>> GetAllAsync()
    {
        return await packageRepository.GetAllAsync();
    }

    public async Task<PaginatedList<Package>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessId, Guid? packageTypeId)
    {
        return await packageRepository.GetPagedAsync(pageIndex, pageSize, search, businessId, packageTypeId);
    }

    public async Task<Package?> GetByIdAsync(Guid id)
    {
        return await packageRepository.GetByIdAsync(id);
    }

    public async Task<List<Package>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        return await packageRepository.GetByIdsAsync(ids);
    }

    public async Task AddAsync(Package package)
    {
        await EnsureCanManageBusinessAsync(package.BusinessId);

        await packageRepository.AddAsync(package);
        await packageRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Package package)
    {
        var packageFromDb = await packageRepository.GetByIdAsync(package.Id);
        if (packageFromDb is null)
            return;

        await EnsureCanManageBusinessAsync(packageFromDb.BusinessId);

        UpdatePackage(package, packageFromDb);
        await packageRepository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Package package)
    {
        var packageFromDb = await packageRepository.GetByIdAsync(package.Id);
        if (packageFromDb is null)
            return;

        await EnsureCanManageBusinessAsync(packageFromDb.BusinessId);

        await packageRepository.DeleteAsync(package.Id);
        await packageRepository.SaveChangesAsync();
    }

    private async Task EnsureCanManageBusinessAsync(Guid businessId)
    {
        var (isAdmin, userId) = await currentUser.GetCurrentUserAsync();
        if (isAdmin)
            return;

        var business = await businessService.GetByIdAsync(businessId);
        if (business is null || business.ManagerId != userId)
            throw new UnauthorizedAccessException("You can only manage packages that belong to your business.");
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