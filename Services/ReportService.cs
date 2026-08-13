using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class ReportService(
    IReportRepository reportRepository,
    IBusinessService businessService,
    IPackageService packageService,
    IAuditLogService auditLogService,
    EcoMealDbContext dbContext,
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
        await currentUser.EnsureAdminAsync("Only an admin can manage reports.");

        var reports = await reportRepository.GetByStatusAsync(ReportStatuses.Open);

        // Batch-resolve target names in (at most) two queries instead of one heavy per-report
        // lookup each — ResolveTargetNameAsync's single-target GetByIdAsync calls are fine for the
        // Dismiss/TakeAction paths below, but looping them here turned every open-reports page
        // load into dozens of full-graph queries.
        var businessIds = reports.Where(r => r.TargetType == AuditTargetTypes.Business).Select(r => r.TargetId).Distinct().ToList();
        var packageIds = reports.Where(r => r.TargetType != AuditTargetTypes.Business).Select(r => r.TargetId).Distinct().ToList();

        var businessNames = businessIds.Count > 0 ? await businessService.GetNamesByIdsAsync(businessIds) : [];
        var packageNames = packageIds.Count > 0 ? await packageService.GetNamesByIdsAsync(packageIds) : [];

        return reports.Select(report => new ReportView(
            report,
            report.TargetType == AuditTargetTypes.Business
                ? businessNames.GetValueOrDefault(report.TargetId, "(deleted business)")
                : packageNames.GetValueOrDefault(report.TargetId, "(deleted package)"),
            report.Reporter.Name)).ToList();
    }

    public async Task DismissAsync(Guid reportId)
    {
        await currentUser.EnsureAdminAsync("Only an admin can manage reports.");

        var report = await reportRepository.GetByIdAsync(reportId);
        if (report is null || report.Status != ReportStatuses.Open)
            return;

        var targetName = await ResolveTargetNameAsync(report);

        await ResolveAsync(report, ReportStatuses.Dismissed);
        await auditLogService.LogAsync(AuditActions.ReportDismissed, report.TargetType, report.TargetId.ToString(), targetName, report.Reason);
    }

    public async Task TakeActionAsync(Guid reportId, string actionReason)
    {
        await currentUser.EnsureAdminAsync("Only an admin can manage reports.");

        var report = await reportRepository.GetByIdAsync(reportId);
        if (report is null || report.Status != ReportStatuses.Open)
            return;

        // Hiding the target, resolving the report, and the audit log entry are each their own
        // SaveChangesAsync — wrap them in one transaction so a failure partway through (e.g. a concurrent
        // admin resolving the same report) can't leave the target hidden but the report still Open.
        // Notification is deliberately excluded (notify: false) and sent after commit — it fans out a
        // synchronous outbound push HTTP call per affected staff member, which must not hold these row locks open.
        Business? hiddenBusiness = null;
        Package? hiddenPackage = null;

        await using (var transaction = await dbContext.Database.BeginTransactionAsync())
        {
            if (report.TargetType == AuditTargetTypes.Business)
                hiddenBusiness = await businessService.HideAsync(report.TargetId, actionReason, notify: false);
            else
                hiddenPackage = await packageService.HideAsync(report.TargetId, actionReason, notify: false);

            var targetName = await ResolveTargetNameAsync(report);

            await ResolveAsync(report, ReportStatuses.ActionTaken);
            await auditLogService.LogAsync(AuditActions.ReportActionTaken, report.TargetType, report.TargetId.ToString(), targetName, actionReason);

            await transaction.CommitAsync();
        }

        if (hiddenBusiness is not null)
            await businessService.NotifyHiddenAsync(hiddenBusiness, actionReason);
        else if (hiddenPackage is not null)
            await packageService.NotifyHiddenAsync(hiddenPackage, actionReason);
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
        {
            var names = await businessService.GetNamesByIdsAsync([report.TargetId]);
            return names.GetValueOrDefault(report.TargetId, "(deleted business)");
        }

        var packageNames = await packageService.GetNamesByIdsAsync([report.TargetId]);
        return packageNames.GetValueOrDefault(report.TargetId, "(deleted package)");
    }
}
