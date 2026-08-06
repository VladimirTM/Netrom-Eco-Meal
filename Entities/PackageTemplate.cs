using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

// A "repeat this every day" template — PackageTemplateGenerationService spins off one real
// Package instance per calendar day from each active template.
public class PackageTemplate
{
    public Guid Id { get; set; }
    public required Guid BusinessId { get; set; }
    public required Guid PackageTypeId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required decimal Price { get; set; }
    // Restocked to this amount on every generated instance — not decremented by orders itself.
    public required int Quantity { get; set; }
    public required decimal WeightKg { get; set; }
    public List<string> DietaryTags { get; set; } = [];
    // Daily pickup window as UTC time-of-day, combined with a calendar date at generation time.
    // EndTimeUtc < StartTimeUtc means the window crosses midnight (end falls the next day).
    public required TimeSpan PickupStartTimeUtc { get; set; }
    public required TimeSpan PickupEndTimeUtc { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    // UTC date of the last Package instance generated from this template — guards one generation
    // per calendar day regardless of how often the background sweep runs.
    public DateOnly? LastGeneratedDate { get; set; }
    [ForeignKey(nameof(BusinessId))]
    public Business Business { get; set; } = null!;
    [ForeignKey(nameof(PackageTypeId))]
    public PackageType PackageType { get; set; } = null!;
    public ICollection<Package> GeneratedPackages { get; set; } = [];
}
