using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

// One order can have several of these — each gets its own QR, so a group order can be split
// across whoever's picking it up. Redeeming any one pass completes the whole order (see
// OrderService.RedeemPickupPassAsync); a Confirmed order's passes are therefore always unredeemed.
public class OrderPickupPass
{
    public Guid Id { get; set; }
    public required Guid OrderId { get; set; }
    public required string Label { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RedeemedAt { get; set; }
    [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; } = null!;
}
