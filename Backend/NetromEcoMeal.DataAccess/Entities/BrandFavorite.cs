using System.ComponentModel.DataAnnotations.Schema;

namespace NetromEcoMeal.Entities;

// A customer following a Brand as a whole, not one location — one row per (UserId, BrandId).
public class BrandFavorite
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required Guid BrandId { get; set; }
    public DateTime CreatedAt { get; set; }
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;
    [ForeignKey(nameof(BrandId))]
    public Brand Brand { get; set; } = null!;
}
