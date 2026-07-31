using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;

namespace Netrom_Eco_Meal.Services.Interfaces;

public record RegisterOutcome(string? Error, string? Info);

// Thin wrapper over ASP.NET Identity so AuthController doesn't depend on it directly.
public interface IAuthService
{
    public Task<SignInResult> LoginAsync(LoginRequest request);
    // Error is set on failure; Info is a non-error message on success (e.g. "check your email"),
    // set only when Identity:RequireConfirmedAccount means registering doesn't sign the user in.
    public Task<RegisterOutcome> RegisterAsync(RegisterRequest request, string name);
    public Task LogoutAsync();
    // Returns null on success, or a user-facing error message on failure.
    public Task<string?> ConfirmEmailAsync(string userId, string token);
    // Always succeeds silently — never reveals whether an account exists for the given email.
    public Task RequestPasswordResetAsync(string email);
    public Task<string?> ResetPasswordAsync(string email, string token, string newPassword);
}
