using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

// A report's target, resolved for display — Report.TargetId is polymorphic (Business or
// Package), so there's no navigation property to eagerly load it from.
public record ReportView(Report Report, string TargetName, string ReporterName);

// Submit is open to any signed-in user; Dismiss/TakeAction/GetOpenAsync are admin-only.
public interface IReportService
{
    public Task SubmitAsync(string targetType, Guid targetId, string reason);
    public Task<List<ReportView>> GetOpenAsync();
    public Task DismissAsync(Guid reportId);
    public Task TakeActionAsync(Guid reportId, string actionReason);
}
