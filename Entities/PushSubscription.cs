using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

// A browser's Web Push subscription for one user — one row per subscribed browser/device, so a
// user signed in on several devices gets a push to each. Endpoint is the natural key: the same
// browser resubscribing (permission re-granted, service worker updated) gets a fresh Endpoint,
// so a stale row is pruned by NotificationService rather than reused.
public class PushSubscription
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required string Endpoint { get; set; }
    public required string P256Dh { get; set; }
    public required string Auth { get; set; }
    public DateTime CreatedAt { get; set; }
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;
}
