using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromForm] LoginRequest request, [FromQuery] string? returnUrl)
    {
        var result = await authService.LoginAsync(request);

        if (result.Succeeded)
        {
            return LocalRedirect(returnUrl ?? "/");
        }

        return LocalRedirect($"/account/login?error=Invalid login&returnUrl={returnUrl}");
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync([FromForm] RegisterRequest request, [FromForm] string name, [FromQuery] string? returnUrl)
    {
        var result = await authService.RegisterAsync(request, name);

        if (result.Succeeded)
        {
            return LocalRedirect(returnUrl ?? "/");
        }

        return LocalRedirect($"/account/register?error=Registration failed&returnUrl={returnUrl}");
    }

    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync([FromQuery] string? returnUrl)
    {
        await authService.LogoutAsync();
        return LocalRedirect(returnUrl ?? "/account/login");
    }
}