using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Hit via plain HTML form posts from Login/Register/logout, not injected into pages like the others —
// each embeds <AntiforgeryToken /> for this to validate. Deliberately NOT [ApiController]: its automatic
// invalid-ModelState -> 400 short-circuit fires before the action body, swallowing the empty-field
// redirect below (and RegisterAsync's equivalent).
[Route("api/[controller]")]
public class AuthController(IAuthService authService, SignInManager<ApplicationUser> signInManager) : ControllerBase
{
    // Login/Register stay antiforgery-protected (forging either matters); logout deliberately doesn't (see below).
    [HttpPost("login")]
    [ManualValidateAntiforgeryToken]
    public async Task<IActionResult> LoginAsync([FromForm] LoginRequest request, [FromForm] string? returnUrl)
    {
        if (!ModelState.IsValid)
            return LocalRedirect($"/account/login?error={Uri.EscapeDataString("Enter your email and password.")}&returnUrl={returnUrl}");

        var result = await authService.LoginAsync(request);

        if (result.Succeeded)
        {
            return LocalRedirect(returnUrl ?? "/");
        }

        // IsNotAllowed covers RequireConfirmedAccount rejecting an unconfirmed email — worth telling
        // apart from a plain wrong password, since the fix ("check your inbox") is completely different.
        var message = result.IsNotAllowed
            ? "Confirm your email before signing in — check your inbox for the confirmation link."
            : "Invalid login";

        return LocalRedirect($"/account/login?error={Uri.EscapeDataString(message)}&returnUrl={returnUrl}");
    }

    [HttpPost("register")]
    [ManualValidateAntiforgeryToken]
    public async Task<IActionResult> RegisterAsync([FromForm] RegisterRequest request, [FromForm] string name, [FromForm] string? returnUrl)
    {
        var refill = $"&name={Uri.EscapeDataString(name ?? "")}&email={Uri.EscapeDataString(request.Email ?? "")}";

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(name))
            return LocalRedirect($"/account/register?error={Uri.EscapeDataString("Fill in your name, email, and password.")}{refill}&returnUrl={returnUrl}");

        var outcome = await authService.RegisterAsync(request, name);

        if (outcome.Error is not null)
            return LocalRedirect($"/account/register?error={Uri.EscapeDataString(outcome.Error)}{refill}&returnUrl={returnUrl}");

        // Info (no error) means RequireConfirmedAccount left the user signed out — show "check
        // your email" instead of continuing to returnUrl.
        if (outcome.Info is not null)
            return LocalRedirect($"/account/login?info={Uri.EscapeDataString(outcome.Info)}");

        return LocalRedirect(returnUrl ?? "/");
    }

    // No [ManualValidateAntiforgeryToken]: a forged logout only logs the victim out, and the header's
    // form can render tokenless via NotFoundPage's HttpContext-less in-circuit fallback.
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync([FromQuery] string? returnUrl)
    {
        await authService.LogoutAsync();
        return LocalRedirect(returnUrl ?? "/account/login");
    }

    // A real HTTP form post rather than an in-process call from AccountSettings.razor, unlike
    // UpdateNameAsync below — ChangePasswordAsync rotates the security stamp, and refreshing the
    // auth cookie to match (RefreshSignInAsync, a few lines down) needs a real, not-yet-started
    // HTTP response, which a Blazor Server circuit's own SignalR connection doesn't have.
    [Authorize]
    [HttpPost("change-password")]
    [ManualValidateAntiforgeryToken]
    public async Task<IActionResult> ChangePasswordFormAsync([FromForm] string currentPassword, [FromForm] string newPassword, [FromForm] string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            return LocalRedirect($"/account/settings?pwError={Uri.EscapeDataString("Fill in your current and new password.")}");

        if (newPassword != confirmPassword)
            return LocalRedirect($"/account/settings?pwError={Uri.EscapeDataString("The new password and confirmation don't match.")}");

        var user = await signInManager.UserManager.GetUserAsync(User);
        if (user is null)
            return LocalRedirect($"/account/settings?pwError={Uri.EscapeDataString("You must be signed in.")}");

        var error = await authService.ChangePasswordAsync(user.Id, currentPassword, newPassword);
        if (error is not null)
            return LocalRedirect($"/account/settings?pwError={Uri.EscapeDataString(error)}");

        await signInManager.RefreshSignInAsync(user);

        return LocalRedirect("/account/settings?pwChanged=true");
    }

    // In-process only below: no HTTP verb attribute, so MVC never routes here — called directly by
    // ConfirmEmail/ForgotPassword/ResetPassword like OrderController etc. are injected elsewhere.
    // They don't touch the auth cookie, so they skip the real HTTP round trip login/register/logout need.

    public async Task<string?> ConfirmEmailAsync(string userId, string token) =>
        await authService.ConfirmEmailAsync(userId, token);

    public async Task RequestPasswordResetAsync(string email) =>
        await authService.RequestPasswordResetAsync(email);

    public async Task<string?> ResetPasswordAsync(string email, string token, string newPassword) =>
        await authService.ResetPasswordAsync(email, token, newPassword);

    public async Task<string?> UpdateNameAsync(string newName) =>
        await authService.UpdateNameAsync(newName);
}
