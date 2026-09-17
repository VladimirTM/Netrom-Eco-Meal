using Microsoft.AspNetCore.Identity;

namespace Netrom_Eco_Meal.Entities;

// Extends Identity's built-in user with the display name Identity doesn't provide.
public class ApplicationUser : IdentityUser
{
    public required string Name { get; set; }
    // Opt-in — a customer must explicitly turn this on (from /impact) before their name appears
    // on the community impact leaderboard. Off by default, same privacy-first default as every
    // other opt-in toggle in the app (web push, favorites visibility).
    public bool ShowOnLeaderboard { get; set; }
    // Lazily generated on first visit to /referrals (ReferralService.GetMyReferralInfoAsync) rather
    // than at every account-creation path — null until then. Unique when set; a nullable unique
    // index allows any number of not-yet-generated (null) rows.
    public string? ReferralCode { get; set; }
    public ICollection<Order> Orders { get; set; } = [];
}