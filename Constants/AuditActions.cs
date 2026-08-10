namespace Netrom_Eco_Meal.Constants;

// Values must match the AuditLog.Action rows AuditLogService writes.
public static class AuditActions
{
    public const string RoleChanged = "RoleChanged";
    public const string BusinessCreated = "BusinessCreated";
    public const string BusinessUpdated = "BusinessUpdated";
    public const string BusinessDeleted = "BusinessDeleted";
    public const string BusinessStaffAdded = "BusinessStaffAdded";
    public const string BusinessStaffRemoved = "BusinessStaffRemoved";
    public const string BusinessApplied = "BusinessApplied";
    public const string BusinessApproved = "BusinessApproved";
    public const string BusinessRejected = "BusinessRejected";
    public const string BusinessHidden = "BusinessHidden";
    public const string BusinessUnhidden = "BusinessUnhidden";
    public const string PackageHidden = "PackageHidden";
    public const string PackageUnhidden = "PackageUnhidden";
    public const string ReportDismissed = "ReportDismissed";
    public const string ReportActionTaken = "ReportActionTaken";
    public const string OrderConfirmed = "OrderConfirmed";
    public const string OrderCompleted = "OrderCompleted";
    public const string OrderCancelled = "OrderCancelled";
    public const string OrderNoShow = "OrderNoShow";
}
