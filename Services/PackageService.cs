using System.Net;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

// Write methods are restricted to admins and the package's own business manager.
public class PackageService(
    IPackageRepository packageRepository,
    IBusinessService businessService,
    IFavoriteRepository favoriteRepository,
    INotificationService notificationService,
    IAppEmailSender emailSender,
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

        if (package.Quantity > 0)
            await NotifyFavoritingCustomersAsync(package.BusinessId, $"New package available: \"{package.Name}\".");
    }

    public async Task UpdateAsync(Package package)
    {
        var packageFromDb = await packageRepository.GetByIdAsync(package.Id);
        if (packageFromDb is null)
            return;

        await EnsureCanManageBusinessAsync(packageFromDb.BusinessId);

        // Captured before UpdatePackage overwrites Quantity — "back in stock" only fires on the
        // 0 -> positive transition, not on every edit that leaves stock unchanged or lowers it.
        var wasOutOfStock = packageFromDb.Quantity <= 0;
        var restocked = wasOutOfStock && package.Quantity > 0;

        UpdatePackage(package, packageFromDb);
        await packageRepository.SaveChangesAsync();

        if (restocked)
            await NotifyFavoritingCustomersAsync(packageFromDb.BusinessId, $"Back in stock: \"{packageFromDb.Name}\".");
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
        packageToUpdate.WeightKg = package.WeightKg;
        packageToUpdate.DietaryTags = package.DietaryTags;
        packageToUpdate.PickupStart = package.PickupStart;
        packageToUpdate.PickupEnd = package.PickupEnd;
        packageToUpdate.ImageUrl = package.ImageUrl;
    }

    // Favorites double as a lightweight "notify me" list — there's no per-package subscription
    // model, so a business's favoriters are the closest proxy for "wants to hear about this".
    private async Task NotifyFavoritingCustomersAsync(Guid businessId, string message)
    {
        var favoriters = await favoriteRepository.GetFavoritingUsersAsync(businessId);
        if (favoriters.Count == 0)
            return;

        var business = await businessService.GetByIdAsync(businessId);
        var url = $"/businesses/{businessId}";
        var fullMessage = $"{business?.Name}: {message}";

        foreach (var user in favoriters)
        {
            await notificationService.CreateAsync(user.Id, fullMessage, url);

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                var html = $"<p>Hi {WebUtility.HtmlEncode(user.Name)},</p><p>{WebUtility.HtmlEncode(fullMessage)}</p><p>— Eco Meal</p>";
                await emailSender.SendEmailAsync(user.Email, "Eco Meal — New packages available", html);
            }
        }
    }
}