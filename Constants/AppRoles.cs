namespace Netrom_Eco_Meal.Constants;

// Values must match the ASP.NET Identity role names DbSeeder creates.
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
    public const string BusinessManager = "BusinessManager";
    
    public static readonly string[] AllRoles = [ Admin, Customer, BusinessManager ];
}