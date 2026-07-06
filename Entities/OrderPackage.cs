using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

public class OrderPackage
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid PackageId { get; set; }
    public required int Quantity { get; set; }
    [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; } = null!;
    [ForeignKey(nameof(PackageId))]
    public Package Package { get; set; } = null!;
}