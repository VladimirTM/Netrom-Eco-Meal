using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

public class Order
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required Guid BusinessId { get; set; }
    public required Guid StatusId { get; set; }
    public int OrderNumber { get; set; }
    [ForeignKey(nameof(UserId))]
    public required ApplicationUser User { get; set; }
    [ForeignKey(nameof(BusinessId))]
    public Business Business { get; set; } = null!;
    [ForeignKey(nameof(StatusId))]
    public Status Status { get; set; } = null!;
    public ICollection<OrderPackage> OrderPackages { get; set; } = [];
}