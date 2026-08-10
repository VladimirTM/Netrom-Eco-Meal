using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Hit via plain HTML form posts from Login/Register/logout, not injected into pages like the others.
// Each form embeds <AntiforgeryToken /> so this validation has something to check.
[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    // Login/Register stay antiforgery-protected — forging either has real impact. Logout deliberately
    // doesn't (see below).
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

        return LocalRedirect($"/account/login?error={Uri.EscapeDataString("Invalid login")}&returnUrl={returnUrl}");
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

        // Info set (no error) means RequireConfirmedAccount left the user signed out — show the
        // "check your email" message on the login page instead of continuing to returnUrl.
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

    // In-process only below: no HTTP verb attribute, so MVC never routes to these — called
    // directly by ConfirmEmail/ForgotPassword/ResetPassword the same way OrderController etc. are
    // injected into other pages. They don't touch the auth cookie, so they don't need the real
    // HTTP round trip login/register/logout require.

    public async Task<string?> ConfirmEmailAsync(string userId, string token) =>
        await authService.ConfirmEmailAsync(userId, token);

    public async Task RequestPasswordResetAsync(string email) =>
        await authService.RequestPasswordResetAsync(email);

    public async Task<string?> ResetPasswordAsync(string email, string token, string newPassword) =>
        await authService.ResetPasswordAsync(email, token, newPassword);
}
