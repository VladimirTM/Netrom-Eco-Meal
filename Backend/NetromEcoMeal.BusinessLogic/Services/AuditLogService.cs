using Microsoft.AspNetCore.Identity;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Models;
using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Services;

public class AuditLogService(
    IAuditLogRepository auditLogRepository,
    UserManager<ApplicationUser> userManager,
    ICurrentUser currentUser) : IAuditLogService
{
    public async Task LogAsync(string action, string targetType, string? targetId, string targetName, string? details = null)
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            return;

        var actor = await userManager.FindByIdAsync(userId);

        await auditLogRepository.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = userId,
            ActorName = actor?.Name ?? "Unknown",
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            TargetName = targetName,
            Details = details,
            CreatedAt = DateTime.UtcNow,
        });
    }

    public async Task<PaginatedList<AuditLog>> GetPagedAsync(int pageIndex, int pageSize, string? action, string? targetType, string? search)
    {
        await currentUser.EnsureAdminAsync("Only an admin can view the audit log.");

        return await auditLogRepository.GetPagedAsync(pageIndex, pageSize, action, targetType, search);
    }
}
