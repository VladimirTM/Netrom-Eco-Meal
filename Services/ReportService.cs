using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class ReportService(
    IReportRepository reportRepository,
    IBusinessService businessService,
    IPackageService packageService,
    IAuditLogService auditLogService,
    CurrentUserAccessor currentUser) : IReportService
{
    public async Task SubmitAsync(string targetType, Guid targetId, string reason)
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to report something.");

        await reportRepository.AddAsync(new Report
        {
            Id = Guid.NewGuid(),
            ReporterUserId = userId,
            TargetType = targetType,
            TargetId = targetId,
            Reason = reason,
            Status = ReportStatuses.Open,
            CreatedAt = DateTime.UtcNow,
        });
    }

    public async Task<List<ReportView>> GetOpenAsync()
    {
        await EnsureAdminAsync();

        var reports = await reportRepository.GetByStatusAsync(ReportStatuses.Open);
        var views = new List<ReportView>();
        foreach (var report in reports)
            views.Add(new ReportView(report, await ResolveTargetNameAsync(report), report.Reporter.Name));

        return views;
    }

    public async Task DismissAsync(Guid reportId)
    {
        await EnsureAdminAsync();

        var report = await reportRepository.GetByIdAsync(reportId);
        if (report is null || report.Status != ReportStatuses.Open)
            return;

        var targetName = await ResolveTargetNameAsync(report);

        await ResolveAsync(report, ReportStatuses.Dismissed);
        await auditLogService.LogAsync(AuditActions.ReportDismissed, report.TargetType, report.TargetId.ToString(), targetName, report.Reason);
    }

    public async Task TakeActionAsync(Guid reportId, string actionReason)
    {
        await EnsureAdminAsync();

        var report = await reportRepository.GetByIdAsync(reportId);
        if (report is null || report.Status != ReportStatuses.Open)
            return;

        if (report.TargetType == AuditTargetTypes.Business)
            await businessService.HideAsync(report.TargetId, actionReason);
        else
            await packageService.HideAsync(report.TargetId, actionReason);

        var targetName = await ResolveTargetNameAsync(report);

        await ResolveAsync(report, ReportStatuses.ActionTaken);
        await auditLogService.LogAsync(AuditActions.ReportActionTaken, report.TargetType, report.TargetId.ToString(), targetName, actionReason);
    }

    private async Task ResolveAsync(Report report, string status)
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();

        report.Status = status;
        report.ResolvedAt = DateTime.UtcNow;
        report.ResolvedByUserId = userId;
        await reportRepository.SaveChangesAsync();
    }

    private async Task<string> ResolveTargetNameAsync(Report report)
    {
        if (report.TargetType == AuditTargetTypes.Business)
            return (await businessService.GetByIdAsync(report.TargetId))?.Name ?? "(deleted business)";

        return (await packageService.GetByIdAsync(report.TargetId))?.Name ?? "(deleted package)";
    }

    private async Task EnsureAdminAsync()
    {
        var (isAdmin, _) = await currentUser.GetCurrentUserAsync();
        if (!isAdmin)
            throw new UnauthorizedAccessException("Only an admin can manage reports.");
    }
}
