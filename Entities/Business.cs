using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

public class Business
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Address { get; set; }
    public string? ImageUrl { get; set; }
    public Guid BusinessTypeId { get; set; }
    [ForeignKey(nameof(BusinessTypeId))]
    public BusinessType BusinessType { get; set; } = null!;
    // Nullable/unique: a manager oversees at most one business (enforced by a unique index).
    public string? ManagerId { get; set; }
    [ForeignKey(nameof(ManagerId))]
    public ApplicationUser? Manager { get; set; }
    public ICollection<Package> Packages { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
}