using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

// One row per successful sign-up through a referral link — created at registration
// (AuthService.RegisterAsync via IReferralService.RegisterReferralAsync), rewarded once
// (RewardedAt set) the moment the invitee's first order ever reaches Completed
// (OrderService.ApplyStatusChangeAsync via IReferralService.TryRewardFirstCompletionAsync).
public class Referral
{
    public Guid Id { get; set; }
    public required string ReferrerUserId { get; set; }
    public required string ReferredUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RewardedAt { get; set; }
    [ForeignKey(nameof(ReferrerUserId))]
    public ApplicationUser Referrer { get; set; } = null!;
    [ForeignKey(nameof(ReferredUserId))]
    public ApplicationUser Referred { get; set; } = null!;
}
