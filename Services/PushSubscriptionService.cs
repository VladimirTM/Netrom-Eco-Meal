using Microsoft.Extensions.Logging;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class PushSubscriptionService(
    IPushSubscriptionRepository pushSubscriptionRepository,
    IWebPushGateway webPushGateway,
    CurrentUserAccessor currentUser,
    ILogger<PushSubscriptionService> logger) : IPushSubscriptionService
{
    public string? GetPublicKey() => webPushGateway.IsConfigured ? webPushGateway.PublicKey : null;

    public async Task SubscribeAsync(string endpoint, string p256dh, string auth)
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to enable push notifications.");

        await pushSubscriptionRepository.AddAsync(userId, endpoint, p256dh, auth);
    }

    public async Task UnsubscribeAsync(string endpoint)
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            return;

        // Only removes a subscription owned by the caller — RemoveByEndpointAsync is intentionally
        // unscoped (it also backs SendToUserAsync's prune-on-410 path below), so the check happens here.
        var mySubscriptions = await pushSubscriptionRepository.GetByUserIdAsync(userId);
        if (mySubscriptions.Any(s => s.Endpoint == endpoint))
            await pushSubscriptionRepository.RemoveByEndpointAsync(endpoint);
    }

    public async Task SendToUserAsync(string userId, string message, string? url)
    {
        if (!webPushGateway.IsConfigured)
            return;

        // Best-effort like WebPushGateway.SendAsync itself (see IPushSubscriptionService) — a
        // transient failure reading/pruning subscriptions must not surface past NotificationService
        // .CreateAsync and fail whatever business action triggered the notification.
        try
        {
            var subscriptions = await pushSubscriptionRepository.GetByUserIdAsync(userId);
            foreach (var subscription in subscriptions)
            {
                var stillValid = await webPushGateway.SendAsync(subscription, "Eco Meal", message, url);
                if (!stillValid)
                    await pushSubscriptionRepository.RemoveByEndpointAsync(subscription.Endpoint);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Push delivery failed for user {UserId}", userId);
        }
    }
}
