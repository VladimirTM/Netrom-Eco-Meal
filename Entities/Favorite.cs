using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

// A customer following a business — one row per (UserId, BusinessId), enforced by a unique index.
public class Favorite
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required Guid BusinessId { get; set; }
    public DateTime CreatedAt { get; set; }
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;
    [ForeignKey(nameof(BusinessId))]
    public Business Business { get; set; } = null!;
}
