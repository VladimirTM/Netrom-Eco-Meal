using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

// A shared basket split across several payers against one underlying Order — the organizer starts
// it from CartService, invites others by link/QR, and each participant (including the organizer)
// pays their own ShareAmount through their own Stripe Checkout session. ParticipantCount is fixed
// at creation; TotalAmount / ParticipantCount is each unclaimed slot's price. See
// RescueCircleService and OrderService.ApplyStatusChangeAsync's Pending->Confirmed gate.
public class RescueCircle
{
    public Guid Id { get; set; }
    public required Guid OrderId { get; set; }
    public required string OrganizerId { get; set; }
    public required int ParticipantCount { get; set; }
    public required decimal TotalAmount { get; set; }
    // See Constants.RescueCircleStatuses. "Fully paid" is deliberately not its own stored status —
    // it's derived from Participants (Count == ParticipantCount && every PaidAt set) so it can
    // never drift out of sync with the rows that actually decide it.
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; } = null!;
    [ForeignKey(nameof(OrganizerId))]
    public ApplicationUser Organizer { get; set; } = null!;
    public ICollection<RescueCircleParticipant> Participants { get; set; } = [];
}
