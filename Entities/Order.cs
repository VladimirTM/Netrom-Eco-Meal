using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

public class Order
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required Guid BusinessId { get; set; }
    public required Guid StatusId { get; set; }
    // Assigned by the order_numbers DB sequence on insert — never set this manually.
    public int OrderNumber { get; set; }
    // Used by the stale-Pending expiry sweep to decide when a reservation has gone unconfirmed too long.
    public DateTime CreatedAt { get; set; }
    // Set once OrderLifecycleSweepService sends the "pickup closes soon" reminder, so it isn't sent twice.
    public DateTime? PickupReminderSentAt { get; set; }
    [ForeignKey(nameof(UserId))]
    public required ApplicationUser User { get; set; }
    [ForeignKey(nameof(BusinessId))]
    public Business Business { get; set; } = null!;
    [ForeignKey(nameof(StatusId))]
    public Status Status { get; set; } = null!;
    public ICollection<OrderPackage> OrderPackages { get; set; } = [];
}