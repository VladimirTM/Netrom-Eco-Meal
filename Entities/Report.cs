using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

// A customer flag on a business or package. An admin resolves it either by dismissing (no issue
// found) or taking action (hides the target via BusinessService/PackageService.HideAsync, which
// logs its own AuditLog entry). TargetId is polymorphic (Business or Package) per TargetType, so
// there's deliberately no FK/navigation to the target itself.
public class Report
{
    public Guid Id { get; set; }
    public required string ReporterUserId { get; set; }
    [ForeignKey(nameof(ReporterUserId))]
    public ApplicationUser Reporter { get; set; } = null!;
    // See Constants.AuditTargetTypes (Business or Package).
    public required string TargetType { get; set; }
    public required Guid TargetId { get; set; }
    public required string Reason { get; set; }
    // See Constants.ReportStatuses.
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedByUserId { get; set; }
}
