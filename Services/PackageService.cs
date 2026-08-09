using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Email;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

// Write methods are restricted to admins and the package's own business manager.
public class PackageService(
    IPackageRepository packageRepository,
    IBusinessService businessService,
    IFavoriteRepository favoriteRepository,
    INotificationService notificationService,
    IAppEmailSender emailSender,
    CurrentUserAccessor currentUser,
    IConfiguration configuration,
    IAuditLogService auditLogService) : IPackageService
{
    // Same fallback/config-key convention as AuthService.BaseUrl — used to build an absolute
    // link for the back-in-stock email CTA.
    private string BaseUrl => (configuration["App:BaseUrl"] ?? "http://localhost:8080").TrimEnd('/');

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

    // Copies name/price/tags/pickup window as-is, for a manager to adjust afterward. TemplateId
    // is deliberately not copied — a duplicate is a standalone package, not another template instance.
    public async Task<List<Package>> DuplicateManyAsync(List<Guid> packageIds)
    {
        var packages = await packageRepository.GetByIdsAsync(packageIds);
        await EnsureCanManageBusinessesAsync(packages);

        var duplicates = packages.Select(p => new Package
        {
            Id = Guid.NewGuid(),
            BusinessId = p.BusinessId,
            PackageTypeId = p.PackageTypeId,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            Quantity = p.Quantity,
            WeightKg = p.WeightKg,
            DietaryTags = [..p.DietaryTags],
            PickupStart = p.PickupStart,
            PickupEnd = p.PickupEnd,
            ImageUrl = p.ImageUrl,
        }).ToList();

        foreach (var duplicate in duplicates)
            await packageRepository.AddAsync(duplicate);
        await packageRepository.SaveChangesAsync();

        return duplicates;
    }

    // Relative, not absolute — "add 5 more" works the same regardless of current stock. Clamped
    // at 0, same floor a single-package edit enforces.
    public async Task AdjustQuantityManyAsync(List<Guid> packageIds, int delta)
    {
        var packages = await packageRepository.GetByIdsAsync(packageIds);
        await EnsureCanManageBusinessesAsync(packages);

        foreach (var package in packages)
            package.Quantity = Math.Max(0, package.Quantity + delta);

        await packageRepository.SaveChangesAsync();
    }

    // Pushes PickupEnd later, leaving PickupStart untouched — matches "extend" rather than shifting the whole window.
    public async Task ExtendPickupWindowManyAsync(List<Guid> packageIds, TimeSpan extension)
    {
        var packages = await packageRepository.GetByIdsAsync(packageIds);
        await EnsureCanManageBusinessesAsync(packages);

        foreach (var package in packages)
            package.PickupEnd += extension;

        await packageRepository.SaveChangesAsync();
    }

    public async Task<List<Package>> GetForAnalyticsAsync(Guid? businessId, DateTime since)
    {
        var (isAdmin, userId) = await currentUser.GetCurrentUserAsync();
        if (!isAdmin)
        {
            if (userId is null || businessId is null || !await businessService.IsStaffAsync(businessId.Value, userId))
                throw new UnauthorizedAccessException("You can only view analytics for your own business.");
        }

        return await packageRepository.GetForAnalyticsAsync(businessId, since);
    }

    public async Task HideAsync(Guid packageId, string reason)
    {
        var package = await packageRepository.GetByIdAsync(packageId);
        if (package is null)
            return;

        await EnsureCanManageBusinessAsync(package.BusinessId);

        package.IsHidden = true;
        package.HiddenReason = reason;
        await packageRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.PackageHidden, AuditTargetTypes.Package, package.Id.ToString(), package.Name, reason);
    }

    public async Task UnhideAsync(Guid packageId)
    {
        var package = await packageRepository.GetByIdAsync(packageId);
        if (package is null)
            return;

        await EnsureCanManageBusinessAsync(package.BusinessId);

        package.IsHidden = false;
        package.HiddenReason = null;
        await packageRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.PackageUnhidden, AuditTargetTypes.Package, package.Id.ToString(), package.Name);
    }

    private async Task EnsureCanManageBusinessAsync(Guid businessId)
    {
        var (isAdmin, userId) = await currentUser.GetCurrentUserAsync();
        if (isAdmin)
            return;

        if (userId is null || !await businessService.IsStaffAsync(businessId, userId))
            throw new UnauthorizedAccessException("You can only manage packages that belong to your business.");
    }

    private async Task EnsureCanManageBusinessesAsync(IEnumerable<Package> packages)
    {
        foreach (var businessId in packages.Select(p => p.BusinessId).Distinct())
            await EnsureCanManageBusinessAsync(businessId);
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
                var html = EmailTemplateBuilder.Build(
                    message,
                    [$"You're following {business?.Name} on Eco Meal — here's what just changed."],
                    eyebrow: business?.Name,
                    ctaLabel: "View this kitchen",
                    ctaUrl: $"{BaseUrl}{url}");
                await emailSender.SendEmailAsync(user.Email, "Eco Meal — New packages available", html);
            }
        }
    }
}