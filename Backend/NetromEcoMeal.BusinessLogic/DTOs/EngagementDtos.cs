using NetromEcoMeal.Entities;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.DTOs;

public record NotificationDto(Guid Id, string Message, string? Url, bool IsRead, DateTime CreatedAt)
{
    public static NotificationDto FromEntity(Notification n) => new(n.Id, n.Message, n.Url, n.IsRead, n.CreatedAt);
}

public record SubscribePushRequestDto(string Endpoint, string P256dh, string Auth);

public record UnsubscribePushRequestDto(string Endpoint);

public record StandingOrderDto(
    Guid Id, Guid BusinessId, string BusinessName, Guid? PackageTypeId, string? PackageTypeName,
    string? DietaryTag, decimal MaxWeeklySpend, bool IsActive, DateTime CreatedAt)
{
    public static StandingOrderDto FromEntity(StandingOrder s) => new(
        s.Id, s.BusinessId, s.Business?.Name ?? "", s.PackageTypeId, s.PackageType?.Name,
        s.DietaryTag, s.MaxWeeklySpend, s.IsActive, s.CreatedAt);
}

public record CreateStandingOrderRequestDto(Guid BusinessId, Guid? PackageTypeId, string? DietaryTag, decimal MaxWeeklySpend);

public record UpdateStandingOrderRequestDto(decimal MaxWeeklySpend, bool IsActive);

public record SetImpactOptInRequestDto(bool ShowOnLeaderboard);

public record SubmitReportRequestDto(string TargetType, Guid TargetId, string Reason);

public record ReportViewDto(Guid Id, string TargetType, Guid TargetId, string TargetName, string Reason, string Status, string ReporterName, DateTime CreatedAt, DateTime? ResolvedAt)
{
    public static ReportViewDto FromModel(ReportView view) => new(
        view.Report.Id, view.Report.TargetType, view.Report.TargetId, view.TargetName, view.Report.Reason,
        view.Report.Status, view.ReporterName, view.Report.CreatedAt, view.Report.ResolvedAt);
}

public record TakeReportActionRequestDto(string ActionReason);

public record AuditLogDto(Guid Id, string ActorUserId, string ActorName, string Action, string TargetType, string? TargetId, string TargetName, string? Details, DateTime CreatedAt)
{
    public static AuditLogDto FromEntity(AuditLog log) => new(
        log.Id, log.ActorUserId, log.ActorName, log.Action, log.TargetType, log.TargetId, log.TargetName, log.Details, log.CreatedAt);
}
