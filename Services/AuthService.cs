using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Options;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

// Thin wrapper over ASP.NET Identity so AuthController doesn't depend on it directly.
public class AuthService(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IOptions<IdentityOptions> identityOptions,
    IAppEmailSender emailSender,
    IConfiguration configuration) : IAuthService
{
    // Absolute origin needed for email links — a Blazor page has no notion of "the app's public
    // URL" outside of an active HTTP request, and the background sweep has no request at all.
    private string BaseUrl => (configuration["App:BaseUrl"] ?? "http://localhost:8080").TrimEnd('/');

    public async Task<SignInResult> LoginAsync(LoginRequest request)
    {
        return await signInManager.PasswordSignInAsync(request.Email, request.Password, true, false);
    }

    public async Task<RegisterOutcome> RegisterAsync(RegisterRequest request, string name)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            return new RegisterOutcome("An account with this email already exists.", null);
        }

        var requireConfirmed = identityOptions.Value.SignIn.RequireConfirmedAccount;

        var user = new ApplicationUser
        {
            Name           = name,
            UserName       = request.Email,
            Email          = request.Email,
            EmailConfirmed = !requireConfirmed,
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return new RegisterOutcome(string.Join(" ", result.Errors.Select(e => e.Description)), null);
        }

        // Self-registration always starts as Customer; only an admin can promote from here.
        await userManager.AddToRoleAsync(user, AppRoles.Customer);

        if (requireConfirmed)
        {
            await SendConfirmationEmailAsync(user);
            return new RegisterOutcome(null, "Account created! Check your email to confirm your account before signing in.");
        }

        var signInResult = await signInManager.PasswordSignInAsync(request.Email, request.Password, true, false);
        return signInResult.Succeeded
            ? new RegisterOutcome(null, null)
            : new RegisterOutcome("Account created, but automatic sign-in failed. Please log in.", null);
    }

    public async Task LogoutAsync()
    {
        await signInManager.SignOutAsync();
    }

    public async Task<string?> ConfirmEmailAsync(string userId, string token)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return "This confirmation link is invalid or has expired.";

        var result = await userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded ? null : "This confirmation link is invalid or has expired.";
    }

    public async Task RequestPasswordResetAsync(string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        // Don't reveal whether an account exists for this email.
        if (user is null)
            return;

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var link = $"{BaseUrl}/account/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
        var html = $"<p>Hi {WebUtility.HtmlEncode(user.Name)},</p>" +
                   "<p>Someone requested a password reset for your Eco Meal account. If this was you, click below to choose a new password:</p>" +
                   $"<p><a href=\"{link}\">Reset my password</a></p>" +
                   "<p>If you didn't request this, you can ignore this email.</p>";
        await emailSender.SendEmailAsync(email, "Eco Meal — Reset your password", html);
    }

    public async Task<string?> ResetPasswordAsync(string email, string token, string newPassword)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return "This reset link is invalid or has expired.";

        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded ? null : string.Join(" ", result.Errors.Select(e => e.Description));
    }

    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = $"{BaseUrl}/account/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        var html = $"<p>Welcome to Eco Meal, {WebUtility.HtmlEncode(user.Name)}!</p>" +
                   "<p>Confirm your account to start rescuing surplus food:</p>" +
                   $"<p><a href=\"{link}\">Confirm my account</a></p>";
        await emailSender.SendEmailAsync(user.Email!, "Eco Meal — Confirm your account", html);
    }
}
