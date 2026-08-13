using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class PackageTypeService(
    IPackageTypeRepository packageTypeRepository,
    CurrentUserAccessor currentUser,
    IAuditLogService auditLogService) : IPackageTypeService
{
    public async Task<List<PackageType>> GetAllAsync()
    {
        return await packageTypeRepository.GetAllAsync();
    }

    public async Task AddAsync(PackageType packageType)
    {
        await currentUser.EnsureAdminAsync("Only an admin can manage package types.");

        packageType.Id = Guid.NewGuid();
        packageType.Name = packageType.Name.Trim();
        await packageTypeRepository.AddAsync(packageType);
        await packageTypeRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.PackageTypeCreated, AuditTargetTypes.PackageType, packageType.Id.ToString(), packageType.Name);
    }

    public async Task UpdateAsync(PackageType packageType)
    {
        await currentUser.EnsureAdminAsync("Only an admin can manage package types.");

        var existing = await packageTypeRepository.GetByIdAsync(packageType.Id);
        if (existing is null)
            return;

        var previousName = existing.Name;
        existing.Name = packageType.Name.Trim();
        await packageTypeRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.PackageTypeUpdated, AuditTargetTypes.PackageType, existing.Id.ToString(), existing.Name, $"{previousName} → {existing.Name}");
    }

    public async Task DeleteAsync(Guid id)
    {
        await currentUser.EnsureAdminAsync("Only an admin can manage package types.");

        var packageType = await packageTypeRepository.GetByIdAsync(id);
        if (packageType is null)
            return;

        if (await packageTypeRepository.IsInUseAsync(id))
            throw new InvalidOperationException($"\"{packageType.Name}\" is still used by at least one package — reassign or remove those first.");

        await packageTypeRepository.DeleteAsync(packageType);
        await packageTypeRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.PackageTypeDeleted, AuditTargetTypes.PackageType, id.ToString(), packageType.Name);
    }
}
