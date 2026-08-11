using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class NotificationService(
    INotificationRepository notificationRepository,
    IPushSubscriptionService pushSubscriptionService,
    CurrentUserAccessor currentUser) : INotificationService
{
    public async Task<List<Notification>> GetMyNotificationsAsync(int take = 20)
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            return [];

        return await notificationRepository.GetRecentByUserIdAsync(userId, take);
    }

    public async Task<int> GetMyUnreadCountAsync()
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            return 0;

        return await notificationRepository.GetUnreadCountAsync(userId);
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            return;

        await notificationRepository.MarkAsReadAsync(id, userId);
    }

    public async Task MarkAllAsReadAsync()
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            return;

        await notificationRepository.MarkAllAsReadAsync(userId);
    }

    public async Task CreateAsync(string userId, string message, string? url = null)
    {
        await notificationRepository.CreateAsync(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Message = message,
            Url = url,
            CreatedAt = DateTime.UtcNow,
        });

        // Push is a best-effort delivery-channel add-on to the bell record above (never throws,
        // see IPushSubscriptionService) — centralized here rather than per-caller like email,
        // since every CreateAsync call site wants the identical title/body/url shape.
        await pushSubscriptionService.SendToUserAsync(userId, message, url);
    }
}
