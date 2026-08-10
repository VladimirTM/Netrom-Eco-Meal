using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class BusinessService(
    IBusinessRepository businessRepository,
    UserManager<ApplicationUser> userManager,
    CurrentUserAccessor currentUser,
    IAuditLogService auditLogService,
    INotificationService notificationService) : IBusinessService
{
    public async Task<List<Business>> GetAllAsync(bool publicOnly = false)
    {
        return await businessRepository.GetAllAsync(publicOnly);
    }

    public async Task<PaginatedList<Business>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessTypeId, string? staffUserId = null, string? sortBy = null, bool favoritesOnly = false, double? customerLat = null, double? customerLng = null, string? statusFilter = null, bool publicOnly = false)
    {
        string? favoritedByUserId = null;
        if (favoritesOnly)
        {
            var (_, userId) = await currentUser.GetCurrentUserAsync();
            // "" never matches a real UserId, so a signed-out user's Any() filter comes back empty.
            favoritedByUserId = userId ?? "";
        }

        return await businessRepository.GetPagedAsync(pageIndex, pageSize, search, businessTypeId, staffUserId, sortBy, favoritedByUserId, customerLat, customerLng, statusFilter, publicOnly);
    }

    public async Task<Business?> GetByIdAsync(Guid id)
    {
        return await businessRepository.GetByIdAsync(id);
    }

    public async Task<List<Business>> GetByStaffUserIdAsync(string userId)
    {
        return await businessRepository.GetByStaffUserIdAsync(userId);
    }

    public async Task<List<ApplicationUser>> GetStaffAsync(Guid businessId)
    {
        return await businessRepository.GetStaffAsync(businessId);
    }

    public async Task<bool> IsStaffAsync(Guid businessId, string userId)
    {
        return await businessRepository.IsStaffAsync(businessId, userId);
    }

    public async Task AddAsync(Business business)
    {
        await EnsureAdminAsync();

        business.Status = BusinessStatuses.Approved;
        await businessRepository.AddAsync(business);
        await businessRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.BusinessCreated, AuditTargetTypes.Business, business.Id.ToString(), business.Name);
    }

    public async Task UpdateAsync(Business business)
    {
        var businessFromDb = await businessRepository.GetByIdAsync(business.Id);
        if (businessFromDb is null)
            return;

        var (isAdmin, userId) = await currentUser.GetCurrentUserAsync();
        if (!isAdmin && (userId is null || !await businessRepository.IsStaffAsync(businessFromDb.Id, userId)))
            throw new UnauthorizedAccessException("You can only edit your own business.");

        UpdateBusiness(business, businessFromDb);
        await businessRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.BusinessUpdated, AuditTargetTypes.Business, business.Id.ToString(), businessFromDb.Name);
    }

    public async Task DeleteAsync(Business business)
    {
        await EnsureAdminAsync();

        await businessRepository.DeleteAsync(business.Id);
        await businessRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.BusinessDeleted, AuditTargetTypes.Business, business.Id.ToString(), business.Name);
    }

    public async Task<bool> AddStaffAsync(Guid businessId, string userId, string? userName = null)
    {
        await EnsureAdminAsync();

        bool added;
        try
        {
            added = await businessRepository.AddStaffAsync(businessId, userId);
        }
        catch (DbUpdateException)
        {
            // A concurrent assignment already added this pair.
            return false;
        }

        if (added)
        {
            var business = await businessRepository.GetByIdAsync(businessId);
            await auditLogService.LogAsync(AuditActions.BusinessStaffAdded, AuditTargetTypes.Business, businessId.ToString(),
                business?.Name ?? businessId.ToString(), $"Added {userName ?? userId} as staff");
        }

        return added;
    }

    public async Task<bool> RemoveStaffAsync(Guid businessId, string userId, string? userName = null)
    {
        await EnsureAdminAsync();

        var removed = await businessRepository.RemoveStaffAsync(businessId, userId);

        if (removed)
        {
            var business = await businessRepository.GetByIdAsync(businessId);
            await auditLogService.LogAsync(AuditActions.BusinessStaffRemoved, AuditTargetTypes.Business, businessId.ToString(),
                business?.Name ?? businessId.ToString(), $"Removed {userName ?? userId} from staff");
        }

        return removed;
    }

    public async Task<Business> ApplyAsync(Business business)
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to list a business.");

        business.Status = BusinessStatuses.PendingApproval;
        business.SubmittedByUserId = userId;

        await businessRepository.AddAsync(business);
        await businessRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.BusinessApplied, AuditTargetTypes.Business, business.Id.ToString(), business.Name);

        return business;
    }

    public async Task ApproveAsync(Guid businessId)
    {
        await EnsureAdminAsync();

        var business = await businessRepository.GetByIdAsync(businessId);
        // Allows reconsidering a Rejected application, not just approving a fresh PendingApproval one.
        if (business is null || business.Status == BusinessStatuses.Approved)
            return;

        business.Status = BusinessStatuses.Approved;
        business.RejectionReason = null;
        await businessRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.BusinessApproved, AuditTargetTypes.Business, business.Id.ToString(), business.Name);

        if (business.SubmittedByUserId is not null)
        {
            await GrantApplicantAccessAsync(business, business.SubmittedByUserId);

            await notificationService.CreateAsync(business.SubmittedByUserId,
                $"Your business \"{business.Name}\" was approved! You can now manage it from the Businesses page.", $"/businesses/edit/{business.Id}");
        }
    }

    // Approval alone leaves the applicant unable to reach /businesses/edit/{id} — that page requires
    // the Admin or BusinessManager role plus staff membership, neither of which a self-service
    // applicant (often a plain Customer) has yet.
    private async Task GrantApplicantAccessAsync(Business business, string applicantUserId)
    {
        var applicant = await userManager.FindByIdAsync(applicantUserId);
        if (applicant is null)
            return;

        var roles = await userManager.GetRolesAsync(applicant);
        if (!roles.Contains(AppRoles.Admin) && !roles.Contains(AppRoles.BusinessManager))
        {
            await userManager.RemoveFromRolesAsync(applicant, roles);
            await userManager.AddToRoleAsync(applicant, AppRoles.BusinessManager);
            await auditLogService.LogAsync(AuditActions.RoleChanged, AuditTargetTypes.User, applicant.Id, applicant.Name,
                $"{roles.FirstOrDefault() ?? AppRoles.Customer} → {AppRoles.BusinessManager}");
        }

        await AddStaffAsync(business.Id, applicant.Id, applicant.Name);
    }

    public async Task RejectAsync(Guid businessId, string reason)
    {
        await EnsureAdminAsync();

        var business = await businessRepository.GetByIdAsync(businessId);
        if (business is null || business.Status != BusinessStatuses.PendingApproval)
            return;

        business.Status = BusinessStatuses.Rejected;
        business.RejectionReason = reason;
        await businessRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.BusinessRejected, AuditTargetTypes.Business, business.Id.ToString(), business.Name, reason);

        if (business.SubmittedByUserId is not null)
            await notificationService.CreateAsync(business.SubmittedByUserId,
                $"Your business application \"{business.Name}\" wasn't approved: {reason}", null);
    }

    public async Task HideAsync(Guid businessId, string reason)
    {
        await EnsureAdminAsync();

        var business = await businessRepository.GetByIdAsync(businessId);
        if (business is null || business.Status != BusinessStatuses.Approved)
            return;

        business.IsHidden = true;
        business.HiddenReason = reason;
        await businessRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.BusinessHidden, AuditTargetTypes.Business, business.Id.ToString(), business.Name, reason);

        foreach (var staff in business.Staff)
            await notificationService.CreateAsync(staff.UserId, $"\"{business.Name}\" has been hidden by an admin: {reason}", null);
    }

    public async Task UnhideAsync(Guid businessId)
    {
        await EnsureAdminAsync();

        var business = await businessRepository.GetByIdAsync(businessId);
        if (business is null || business.Status != BusinessStatuses.Approved)
            return;

        business.IsHidden = false;
        business.HiddenReason = null;
        await businessRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.BusinessUnhidden, AuditTargetTypes.Business, business.Id.ToString(), business.Name);
    }

    private async Task EnsureAdminAsync()
    {
        var (isAdmin, _) = await currentUser.GetCurrentUserAsync();
        if (!isAdmin)
            throw new UnauthorizedAccessException("Only an admin can perform this action.");
    }

    private static void UpdateBusiness(Business business, Business businessToUpdate)
    {
        businessToUpdate.Name = business.Name;
        businessToUpdate.Description = business.Description;
        businessToUpdate.Address = business.Address;
        businessToUpdate.ImageUrl = business.ImageUrl;
        businessToUpdate.Latitude = business.Latitude;
        businessToUpdate.Longitude = business.Longitude;
        businessToUpdate.BusinessTypeId = business.BusinessTypeId;
    }
}
