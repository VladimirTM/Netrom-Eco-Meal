using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

public class Package
{
    public Guid Id { get; set; }
    public required Guid BusinessId { get; set; }
    public required Guid PackageTypeId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required decimal Price { get; set; }
    public required int Quantity { get; set; }
    public required DateTime PickupStart { get; set; }
    public required DateTime PickupEnd { get; set; }
    public string? ImageUrl { get; set; }
    [ForeignKey(nameof(BusinessId))]
    public Business Business { get; set; } = null!;
    [ForeignKey(nameof(PackageTypeId))]
    public PackageType PackageType { get; set; } = null!;
    public ICollection<OrderPackage> OrderPackages { get; set; } = [];
}