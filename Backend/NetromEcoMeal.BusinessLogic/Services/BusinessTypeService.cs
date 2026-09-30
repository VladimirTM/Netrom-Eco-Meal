using NetromEcoMeal.Constants;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Services;

public class BusinessTypeService(
    IBusinessTypeRepository businessTypeRepository,
    ICurrentUser currentUser,
    IAuditLogService auditLogService) : IBusinessTypeService
{
    public async Task<List<BusinessType>> GetAllAsync()
    {
        return await businessTypeRepository.GetAllAsync();
    }

    public async Task AddAsync(BusinessType businessType)
    {
        await currentUser.EnsureAdminAsync("Only an admin can manage kitchen types.");

        var name = businessType.Name.Trim();
        // Neither the DB nor any caller enforces uniqueness otherwise — without this, two
        // identically-named types both show up (indistinguishably) in every kitchen-type filter.
        if (await IsNameTakenAsync(name, excludingId: null))
            throw new InvalidOperationException($"A kitchen type named \"{name}\" already exists.");

        businessType.Id = Guid.NewGuid();
        businessType.Name = name;
        await businessTypeRepository.AddAsync(businessType);
        await businessTypeRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.BusinessTypeCreated, AuditTargetTypes.BusinessType, businessType.Id.ToString(), businessType.Name);
    }

    public async Task UpdateAsync(BusinessType businessType)
    {
        await currentUser.EnsureAdminAsync("Only an admin can manage kitchen types.");

        var existing = await businessTypeRepository.GetByIdAsync(businessType.Id);
        if (existing is null)
            return;

        var name = businessType.Name.Trim();
        if (await IsNameTakenAsync(name, excludingId: existing.Id))
            throw new InvalidOperationException($"A kitchen type named \"{name}\" already exists.");

        var previousName = existing.Name;
        existing.Name = name;
        await businessTypeRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.BusinessTypeUpdated, AuditTargetTypes.BusinessType, existing.Id.ToString(), existing.Name, $"{previousName} → {existing.Name}");
    }

    // Case-insensitive — "Bakery" and "bakery" would otherwise both silently exist and both show
    // up as indistinguishable options in every kitchen-type dropdown/filter.
    private async Task<bool> IsNameTakenAsync(string name, Guid? excludingId)
    {
        var all = await businessTypeRepository.GetAllAsync();
        return all.Any(t => t.Id != excludingId && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task DeleteAsync(Guid id)
    {
        await currentUser.EnsureAdminAsync("Only an admin can manage kitchen types.");

        var businessType = await businessTypeRepository.GetByIdAsync(id);
        if (businessType is null)
            return;

        if (await businessTypeRepository.IsInUseAsync(id))
            throw new InvalidOperationException($"\"{businessType.Name}\" is still used by at least one business — reassign or remove those first.");

        await businessTypeRepository.DeleteAsync(businessType);
        await businessTypeRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.BusinessTypeDeleted, AuditTargetTypes.BusinessType, id.ToString(), businessType.Name);
    }
}
