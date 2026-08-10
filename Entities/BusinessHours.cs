using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

// One row per weekday, up to 7 total — always replaced as a full week by SetHoursAsync, never
// added/removed individually. No rows at all means hours haven't been configured yet.
public class BusinessHours
{
    public Guid Id { get; set; }
    public required Guid BusinessId { get; set; }
    public required DayOfWeek DayOfWeek { get; set; }
    public bool IsClosed { get; set; }
    // Local wall-clock time, not UTC — this app has no per-business timezone field.
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
    [ForeignKey(nameof(BusinessId))]
    public Business Business { get; set; } = null!;
}
