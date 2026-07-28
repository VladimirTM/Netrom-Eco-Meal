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
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(name))
            return LocalRedirect($"/account/register?error={Uri.EscapeDataString("Fill in your name, email, and password.")}&returnUrl={returnUrl}");

        var error = await authService.RegisterAsync(request, name);

        if (error is null)
        {
            return LocalRedirect(returnUrl ?? "/");
        }

        return LocalRedirect($"/account/register?error={Uri.EscapeDataString(error)}&returnUrl={returnUrl}");
    }

    // No [ManualValidateAntiforgeryToken]: a forged logout only logs the victim out, and the header's
    // form can render tokenless via NotFoundPage's HttpContext-less in-circuit fallback.
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync([FromQuery] string? returnUrl)
    {
        await authService.LogoutAsync();
        return LocalRedirect(returnUrl ?? "/account/login");
    }
}
