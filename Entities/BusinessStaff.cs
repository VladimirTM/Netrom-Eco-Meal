using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

public class BusinessStaff
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    [ForeignKey(nameof(BusinessId))]
    public Business Business { get; set; } = null!;
    public string UserId { get; set; } = null!;
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;
    public DateTime AssignedAt { get; set; }
}
