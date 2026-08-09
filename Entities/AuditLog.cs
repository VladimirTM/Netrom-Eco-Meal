namespace Netrom_Eco_Meal.Entities;

// Immutable record of admin/manager trust-and-safety actions — role changes, business
// create/edit/delete/staffing, approval decisions, and moderation. Never updated after insert.
public class AuditLog
{
    public Guid Id { get; set; }
    public required string ActorUserId { get; set; }
    public required string ActorName { get; set; }
    // See Constants.AuditActions.
    public required string Action { get; set; }
    // See Constants.AuditTargetTypes.
    public required string TargetType { get; set; }
    public string? TargetId { get; set; }
    // Denormalized so the log stays readable even after the target itself is renamed or deleted.
    public required string TargetName { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
}
