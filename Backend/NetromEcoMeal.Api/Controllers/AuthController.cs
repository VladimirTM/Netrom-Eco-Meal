using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NetromEcoMeal.Constants;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

// Issues JWTs for the React frontend — no cookie, no antiforgery token.
[Route("api/auth")]
[ApiController]
public class AuthController(
    IAuthService authService,
    IJwtTokenService jwtTokenService,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(new AuthErrorDto("Invalid email or password.", "invalid_credentials"));

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (result.IsNotAllowed)
            return StatusCode(StatusCodes.Status403Forbidden,
                new AuthErrorDto("Confirm your email before signing in — check your inbox for the confirmation link.", "email_not_confirmed"));

        if (!result.Succeeded)
            return Unauthorized(new AuthErrorDto("Invalid email or password.", "invalid_credentials"));

        return Ok(await BuildAuthResponseAsync(user));
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<RegisterResponseDto>> Register([FromBody] RegisterRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Conflict("Enter your name.");

        var outcome = await authService.RegisterAsync(
            new RegisterRequest { Email = request.Email, Password = request.Password },
            request.Name,
            request.ReferralCode);

        if (outcome.Error is not null)
            return Conflict(outcome.Error);

        if (outcome.Info is not null)
            return Ok(new RegisterResponseDto(null, null, outcome.Info));

        var response = await BuildAuthResponseAsync(outcome.UserToSignIn!);
        return Ok(new RegisterResponseDto(response.Token, response.User, null));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = userId is null ? null : await userManager.FindByIdAsync(userId);
        if (user is null)
            return Unauthorized();

        return Ok(await ToUserDtoAsync(user));
    }

    [HttpPut("me/name")]
    [Authorize]
    public async Task<IActionResult> UpdateName([FromBody] UpdateNameRequestDto request)
    {
        var error = await authService.UpdateNameAsync(request.Name);
        return error is null ? NoContent() : BadRequest(error);
    }

    [HttpPut("me/password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
            return Unauthorized();

        var error = await authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
        // Not a 401: the caller is already authenticated, and a 401 here would trip the frontend's
        // global logout interceptor even though the account itself is fine.
        return error is null ? NoContent() : BadRequest(error);
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequestDto request)
    {
        var error = await authService.ConfirmEmailAsync(request.UserId, request.Token);
        return error is null ? NoContent() : BadRequest(error);
    }

    [HttpPost("resend-confirmation")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationRequestDto request)
    {
        await authService.ResendConfirmationEmailAsync(request.Email);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        await authService.RequestPasswordResetAsync(request.Email);
        return NoContent();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        var error = await authService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword);
        return error is null ? NoContent() : BadRequest(error);
    }

    private async Task<AuthResponseDto> BuildAuthResponseAsync(ApplicationUser user) =>
        new(await jwtTokenService.GenerateTokenAsync(user), await ToUserDtoAsync(user));

    private async Task<UserDto> ToUserDtoAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new UserDto(user.Id, user.Name, user.Email!, roles.FirstOrDefault() ?? AppRoles.Customer);
    }
}
