using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

// One review per (BusinessId, UserId) — enforced by a unique index; resubmitting updates it in place.
public class Review
{
    public Guid Id { get; set; }
    public required Guid BusinessId { get; set; }
    public required string UserId { get; set; }
    public required int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    // Optional tag to a specific package the reviewer ordered — still one review per business,
    // this just narrows what it's about. Null keeps today's business-level meaning.
    public Guid? PackageId { get; set; }
    [ForeignKey(nameof(BusinessId))]
    public Business Business { get; set; } = null!;
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;
    [ForeignKey(nameof(PackageId))]
    public Package? Package { get; set; }
}
