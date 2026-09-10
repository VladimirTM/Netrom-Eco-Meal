using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

// "Auto-reserve my usual from this kitchen whenever it goes live" — matched against freshly
// generated PackageTemplate instances by StandingOrderService.MatchNewPackagesAsync, rather than
// against every already-live package, so a customer is only ever notified about something new.
public class StandingOrder
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required Guid BusinessId { get; set; }
    // Both optional narrowing filters — null means "any package/tag from this kitchen matches".
    public Guid? PackageTypeId { get; set; }
    public string? DietaryTag { get; set; }
    // A match is only notified while this customer's own spend on matching packages here, over
    // the trailing 7 days, would stay at or under this cap — see StandingOrderService.
    public required decimal MaxWeeklySpend { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;
    [ForeignKey(nameof(BusinessId))]
    public Business Business { get; set; } = null!;
    [ForeignKey(nameof(PackageTypeId))]
    public PackageType? PackageType { get; set; }
}
