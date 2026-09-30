using System.ComponentModel.DataAnnotations.Schema;

namespace NetromEcoMeal.Entities;

// A short practical hint a customer leaves on a business page ("use the side door after 8pm") —
// separate from star Review/Comment, and not scoped to having ordered there. Moderated through
// the same Report/hide pipeline Business/Package already go through (see KitchenTipService.HideAsync).
public class KitchenTip
{
    public Guid Id { get; set; }
    public required Guid BusinessId { get; set; }
    public required string UserId { get; set; }
    public required string Tip { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsHidden { get; set; }
    public string? HiddenReason { get; set; }
    [ForeignKey(nameof(BusinessId))]
    public Business Business { get; set; } = null!;
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;
}
