using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Options;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services.Email;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

// Thin wrapper over ASP.NET Identity so AuthController doesn't depend on it directly.
public class AuthService(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IOptions<IdentityOptions> identityOptions,
    IAppEmailSender emailSender,
    IConfiguration configuration,
    CurrentUserAccessor currentUser) : IAuthService
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
        // ASP.NET Identity's own CreateAsync doesn't validate email *format*, only uniqueness/policy
        // — a syntactically invalid address would otherwise create a real (unconfirmable) user row
        // and then crash SmtpEmailSender's MailAddressCollection parse when sending the confirmation
        // email, since that's the first place anything actually tries to parse it as an address.
        if (!System.Net.Mail.MailAddress.TryCreate(request.Email, out _))
        {
            return new RegisterOutcome("Enter a valid email address.", null);
        }

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
        var html = EmailTemplateBuilder.Build(
            $"Reset your password, {user.Name}",
            [
                "Someone requested a password reset for your Eco Meal account. If this was you, use the button below to choose a new one.",
                "If you didn't request this, you can safely ignore this email — your password hasn't changed.",
            ],
            ctaLabel: "Reset my password",
            ctaUrl: link);
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

    public async Task<string?> UpdateNameAsync(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return "Enter your name.";

        var (_, userId) = await currentUser.GetCurrentUserAsync();
        var user = userId is null ? null : await userManager.FindByIdAsync(userId);
        if (user is null)
            return "You must be signed in.";

        user.Name = newName.Trim();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return string.Join(" ", result.Errors.Select(e => e.Description));

        // Deliberately no SignInManager.RefreshSignInAsync here — it writes a Set-Cookie header,
        // which throws when called mid-circuit from an interactive Blazor Server page (the initial
        // HTTP response has already completed by the time this runs). Not needed anyway: nothing
        // in this app reads the display name from a cookie claim, only from a fresh DB lookup.
        return null;
    }

    // Takes userId explicitly rather than resolving it via CurrentUserAccessor: unlike every other
    // method here, this is only ever called from AuthController's real HTTP change-password action
    // (needed to refresh the auth cookie safely, see that action's own comment) rather than
    // in-process from a Razor component — and CurrentUserAccessor's AuthenticationStateProvider
    // throws when resolved outside an actual Blazor circuit's DI scope.
    public async Task<string?> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return "You must be signed in.";

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
            return string.Join(" ", result.Errors.Select(e => e.Description));

        // No SignInManager refresh here — this stays a plain service method; the caller (a real
        // HTTP action, see above) is the one with a not-yet-started response, so it's the one
        // responsible for refreshing the cookie once this returns successfully.
        return null;
    }

    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = $"{BaseUrl}/account/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        var html = EmailTemplateBuilder.Build(
            $"Welcome, {user.Name}",
            ["Confirm your account to start rescuing surplus food before it's binned."],
            eyebrow: "Get started",
            ctaLabel: "Confirm my account",
            ctaUrl: link);
        await emailSender.SendEmailAsync(user.Email!, "Eco Meal — Confirm your account", html);
    }
}
