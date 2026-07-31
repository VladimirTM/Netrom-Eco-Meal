using System.Net;
using System.Net.Mail;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services.Email;

// Reads Email:Smtp:* straight from IConfiguration (same style as SeedAdmin:Email/Password) rather
// than a bound options class — one small settings surface, not worth the extra ceremony.
// When Email:Smtp:Host isn't configured (e.g. a bare `dotnet run` with no mail server around),
// this logs the email instead of failing — mirrors how DbSeeder just warns and skips when
// SeedAdmin isn't configured, so the app still runs without every piece of optional infra present.
public class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IAppEmailSender
{
    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var host = configuration["Email:Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation("Email:Smtp:Host is not configured — logging instead of sending. To: {To}, Subject: {Subject}\n{Body}",
                toEmail, subject, htmlBody);
            return;
        }

        var port = int.TryParse(configuration["Email:Smtp:Port"], out var parsedPort) ? parsedPort : 25;
        var fromAddress = configuration["Email:FromAddress"] ?? "no-reply@ecomeal.local";
        var fromName = configuration["Email:FromName"] ?? "Eco Meal";
        var username = configuration["Email:Smtp:Username"];
        var password = configuration["Email:Smtp:Password"];
        var enableSsl = bool.TryParse(configuration["Email:Smtp:EnableSsl"], out var parsedSsl) && parsedSsl;

        using var client = new SmtpClient(host, port) { EnableSsl = enableSsl };
        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password);

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(toEmail);

        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            // Deliverability issues shouldn't take down whatever triggered the email (an order
            // status change, a background sweep...) — log and move on.
            logger.LogWarning(ex, "Failed to send email to {To} with subject {Subject}.", toEmail, subject);
        }
    }
}
