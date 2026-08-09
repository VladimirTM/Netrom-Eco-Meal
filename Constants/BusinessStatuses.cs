namespace Netrom_Eco_Meal.Constants;

// Values must match Business.Status set by BusinessService — the admin approval gate for
// self-service business signups (Business.SubmittedByUserId is set). Admin-created businesses
// skip straight to Approved. IsHidden is a separate, orthogonal moderation flag — only an
// Approved business can be hidden/unhidden.
public static class BusinessStatuses
{
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}
