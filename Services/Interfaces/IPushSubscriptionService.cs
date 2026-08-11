namespace Netrom_Eco_Meal.Services.Interfaces;

// Subscribe/Unsubscribe are scoped to the signed-in user's own browser subscription, mirroring
// IFavoriteService's shape; SendToUserAsync is the one exception — called by NotificationService
// to push to *someone else*, same relationship INotificationService.CreateAsync has to its reads.
public interface IPushSubscriptionService
{
    // Null when WebPush:PublicKey/PrivateKey/Subject aren't configured — the frontend hides the
    // enable-push toggle entirely in that case, same degrade-gracefully pattern as Stripe/SMTP.
    string? GetPublicKey();
    Task SubscribeAsync(string endpoint, string p256dh, string auth);
    Task UnsubscribeAsync(string endpoint);
    // Best-effort, never throws — same "never blocks the underlying transition" contract as
    // IAppEmailSender. Prunes any subscription the push service reports as gone.
    Task SendToUserAsync(string userId, string message, string? url);
}
