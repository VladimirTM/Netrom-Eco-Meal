using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class KitchenTipService(
    IKitchenTipRepository kitchenTipRepository,
    INotificationService notificationService,
    IAuditLogService auditLogService,
    CurrentUserAccessor currentUser) : IKitchenTipService
{
    public async Task<List<KitchenTip>> GetVisibleByBusinessIdAsync(Guid businessId)
    {
        return await kitchenTipRepository.GetVisibleByBusinessIdAsync(businessId);
    }

    public async Task<Dictionary<Guid, string>> GetSnippetsByIdsAsync(IEnumerable<Guid> ids)
    {
        return await kitchenTipRepository.GetSnippetsByIdsAsync(ids);
    }

    public async Task<KitchenTip> SubmitAsync(Guid businessId, string tip)
    {
        if (!await currentUser.IsInRoleAsync(AppRoles.Customer))
            throw new UnauthorizedAccessException("Only customers can leave a kitchen tip.");

        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to leave a kitchen tip.");

        var trimmed = tip.Trim();
        if (trimmed.Length == 0)
            throw new InvalidOperationException("Enter a tip before submitting.");
        if (trimmed.Length > KitchenTips.MaxLength)
            trimmed = trimmed[..KitchenTips.MaxLength];

        var entry = new KitchenTip
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            UserId = userId,
            Tip = trimmed,
            CreatedAt = DateTime.UtcNow,
        };

        await kitchenTipRepository.AddAsync(entry);
        await kitchenTipRepository.SaveChangesAsync();
        return entry;
    }

    public async Task<KitchenTip?> HideAsync(Guid tipId, string reason, bool notify = true)
    {
        await currentUser.EnsureAdminAsync();

        var tip = await kitchenTipRepository.GetByIdAsync(tipId);
        if (tip is null)
            return null;

        tip.IsHidden = true;
        tip.HiddenReason = reason;
        await kitchenTipRepository.SaveChangesAsync();

        await auditLogService.LogAsync(AuditActions.KitchenTipHidden, AuditTargetTypes.KitchenTip, tip.Id.ToString(), tip.Tip, reason);

        if (notify)
            await NotifyHiddenAsync(tip, reason);

        return tip;
    }

    public async Task NotifyHiddenAsync(KitchenTip tip, string reason)
    {
        await notificationService.CreateAsync(tip.UserId, $"Your kitchen tip was hidden by an admin: {reason}", null);
    }
}
