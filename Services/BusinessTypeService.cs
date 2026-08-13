using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class BusinessTypeService(
    IBusinessTypeRepository businessTypeRepository,
    CurrentUserAccessor currentUser,
    IAuditLogService auditLogService) : IBusinessTypeService
{
    public async Task<List<BusinessType>> GetAllAsync()
    {
        return await businessTypeRepository.GetAllAsync();
    }

    public async Task AddAsync(BusinessType businessType)
    {
        await currentUser.EnsureAdminAsync("Only an admin can manage kitchen types.");

        businessType.Id = Guid.NewGuid();
        businessType.Name = businessType.Name.Trim();
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

        var previousName = existing.Name;
        existing.Name = businessType.Name.Trim();
        await businessTypeRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.BusinessTypeUpdated, AuditTargetTypes.BusinessType, existing.Id.ToString(), existing.Name, $"{previousName} → {existing.Name}");
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
