namespace Netrom_Eco_Meal.Constants;

// Values must match the ASP.NET Identity role names DbSeeder creates.
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
    public const string BusinessManager = "BusinessManager";
    
    public static readonly string[] AllRoles = [ Admin, Customer, BusinessManager ];

    // The stored value has to stay one PascalCase word (matches the Identity role name), but
    // "BusinessManager" reads as a run-on word wherever it's shown to a user (audit log entries,
    // the sidebar's own role label) — this is the display-only fix-up for that one case.
    public static string Label(string role) => role == BusinessManager ? "Business Manager" : role;
}