using NetromEcoMeal.Constants;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Services;

public class PackageTypeService(
    IPackageTypeRepository packageTypeRepository,
    ICurrentUser currentUser,
    IAuditLogService auditLogService) : IPackageTypeService
{
    public async Task<List<PackageType>> GetAllAsync()
    {
        return await packageTypeRepository.GetAllAsync();
    }

    public async Task AddAsync(PackageType packageType)
    {
        await currentUser.EnsureAdminAsync("Only an admin can manage package types.");

        var name = packageType.Name.Trim();
        // Neither the DB nor any caller enforces uniqueness otherwise — without this, two
        // identically-named types both show up (indistinguishably) in every package-type filter.
        if (await IsNameTakenAsync(name, excludingId: null))
            throw new InvalidOperationException($"A package type named \"{name}\" already exists.");

        packageType.Id = Guid.NewGuid();
        packageType.Name = name;
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

        var name = packageType.Name.Trim();
        if (await IsNameTakenAsync(name, excludingId: existing.Id))
            throw new InvalidOperationException($"A package type named \"{name}\" already exists.");

        var previousName = existing.Name;
        existing.Name = name;
        await packageTypeRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.PackageTypeUpdated, AuditTargetTypes.PackageType, existing.Id.ToString(), existing.Name, $"{previousName} → {existing.Name}");
    }

    // Case-insensitive — "Meal Box" and "meal box" would otherwise both silently exist and both
    // show up as indistinguishable options in every package-type dropdown/filter.
    private async Task<bool> IsNameTakenAsync(string name, Guid? excludingId)
    {
        var all = await packageTypeRepository.GetAllAsync();
        return all.Any(t => t.Id != excludingId && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
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
