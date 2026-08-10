using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

// An inclusive date range that overrides BusinessHours while active — added/removed one at a
// time, unlike BusinessHours' always-replace-the-whole-week semantics.
public class BusinessClosure
{
    public Guid Id { get; set; }
    public required Guid BusinessId { get; set; }
    public required DateOnly StartDate { get; set; }
    public required DateOnly EndDate { get; set; }
    public string? Reason { get; set; }
    [ForeignKey(nameof(BusinessId))]
    public Business Business { get; set; } = null!;
}
