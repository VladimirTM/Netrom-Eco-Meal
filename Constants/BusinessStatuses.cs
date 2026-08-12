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

    // Not a real Status value — a pseudo-status the admin filter (BusinessRepository.GetPagedAsync,
    // Businesses.razor) uses to mean "Status == Approved && IsHidden", since IsHidden is a separate
    // orthogonal moderation flag rather than a Status member.
    public const string HiddenFilter = "Hidden";
}
