using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

// One row per person who's joined a RescueCircle (the organizer included, added automatically when
// the circle is started). Carries its own Stripe fields rather than reusing Payment, since Payment
// is one-per-Order and each participant here pays a separate Checkout session for just their share.
public class RescueCircleParticipant
{
    public Guid Id { get; set; }
    public required Guid RescueCircleId { get; set; }
    public required string UserId { get; set; }
    public required decimal ShareAmount { get; set; }
    public DateTime JoinedAt { get; set; }
    // Set once StartShareCheckoutAsync sends this participant to Stripe; replaced if they retry
    // after an abandoned attempt.
    public string? StripeCheckoutSessionId { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? StripePaymentIntentId { get; set; }
    // Set independently of the whole order's fate — a participant backing out before the circle is
    // fully paid is refunded on their own, without cancelling anyone else's share.
    public DateTime? RefundedAt { get; set; }
    [ForeignKey(nameof(RescueCircleId))]
    public RescueCircle RescueCircle { get; set; } = null!;
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;
}
