namespace Netrom_Eco_Meal.Services.Interfaces;

// Thin wrapper over the WebPush SDK, same shape as IStripeGateway — kept separate so
// NotificationService/PushSubscriptionService don't depend on WebPush's own types directly.
public interface IWebPushGateway
{
    bool IsConfigured { get; }
    string? PublicKey { get; }

    // Returns false only when the push service reports the subscription is gone (404/410) — the
    // caller should delete it. Returns true otherwise: push not configured (no-op), sent
    // successfully, or a transient failure that doesn't mean the subscription itself is dead.
    Task<bool> SendAsync(Entities.PushSubscription subscription, string title, string body, string? url);
}
