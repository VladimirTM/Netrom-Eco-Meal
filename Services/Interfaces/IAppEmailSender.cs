namespace Netrom_Eco_Meal.Services.Interfaces;

// Deliberately not ASP.NET Identity's IEmailSender<TUser> — this app has no Identity Razor Pages
// UI to trigger that interface, and order-lifecycle/back-in-stock emails aren't Identity's concern
// anyway. AuthService calls this directly for confirmation/reset links too.
public interface IAppEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
}
