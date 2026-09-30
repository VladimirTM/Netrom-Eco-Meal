using Microsoft.EntityFrameworkCore;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Database;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Services;

public class ReportService(
    IReportRepository reportRepository,
    IBusinessService businessService,
    IPackageService packageService,
    IKitchenTipService kitchenTipService,
    IAuditLogService auditLogService,
    EcoMealDbContext dbContext,
    ICurrentUser currentUser) : IReportService
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
        var packageIds = reports.Where(r => r.TargetType == AuditTargetTypes.Package).Select(r => r.TargetId).Distinct().ToList();
        var tipIds = reports.Where(r => r.TargetType == AuditTargetTypes.KitchenTip).Select(r => r.TargetId).Distinct().ToList();

        var businessNames = businessIds.Count > 0 ? await businessService.GetNamesByIdsAsync(businessIds) : [];
        var packageNames = packageIds.Count > 0 ? await packageService.GetNamesByIdsAsync(packageIds) : [];
        var tipSnippets = tipIds.Count > 0 ? await kitchenTipService.GetSnippetsByIdsAsync(tipIds) : [];

        return reports.Select(report => new ReportView(report, report.TargetType switch
        {
            AuditTargetTypes.Business => businessNames.GetValueOrDefault(report.TargetId, "(deleted business)"),
            AuditTargetTypes.KitchenTip => tipSnippets.GetValueOrDefault(report.TargetId, "(deleted tip)"),
            _ => packageNames.GetValueOrDefault(report.TargetId, "(deleted package)"),
        }, report.Reporter.Name)).ToList();
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
        KitchenTip? hiddenTip = null;

        await using (var transaction = await dbContext.Database.BeginTransactionAsync())
        {
            switch (report.TargetType)
            {
                case AuditTargetTypes.Business:
                    hiddenBusiness = await businessService.HideAsync(report.TargetId, actionReason, notify: false);
                    break;
                case AuditTargetTypes.KitchenTip:
                    hiddenTip = await kitchenTipService.HideAsync(report.TargetId, actionReason, notify: false);
                    break;
                default:
                    hiddenPackage = await packageService.HideAsync(report.TargetId, actionReason, notify: false);
                    break;
            }

            var targetName = await ResolveTargetNameAsync(report);

            await ResolveAsync(report, ReportStatuses.ActionTaken);
            await auditLogService.LogAsync(AuditActions.ReportActionTaken, report.TargetType, report.TargetId.ToString(), targetName, actionReason);

            await transaction.CommitAsync();
        }

        if (hiddenBusiness is not null)
            await businessService.NotifyHiddenAsync(hiddenBusiness, actionReason);
        else if (hiddenPackage is not null)
            await packageService.NotifyHiddenAsync(hiddenPackage, actionReason);
        else if (hiddenTip is not null)
            await kitchenTipService.NotifyHiddenAsync(hiddenTip, actionReason);
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
        switch (report.TargetType)
        {
            case AuditTargetTypes.Business:
                var businessNames = await businessService.GetNamesByIdsAsync([report.TargetId]);
                return businessNames.GetValueOrDefault(report.TargetId, "(deleted business)");
            case AuditTargetTypes.KitchenTip:
                var tipSnippets = await kitchenTipService.GetSnippetsByIdsAsync([report.TargetId]);
                return tipSnippets.GetValueOrDefault(report.TargetId, "(deleted tip)");
            default:
                var packageNames = await packageService.GetNamesByIdsAsync([report.TargetId]);
                return packageNames.GetValueOrDefault(report.TargetId, "(deleted package)");
        }
    }
}
