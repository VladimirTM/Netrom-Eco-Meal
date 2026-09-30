using Microsoft.AspNetCore.Identity.Data;
using NetromEcoMeal.Entities;

namespace NetromEcoMeal.Services.Interfaces;

// UserToSignIn is set only on a successful registration that doesn't require email confirmation —
// the caller (a real HTTP action; see AuthService's own comment on why this can't sign in itself)
// is responsible for writing the auth cookie via SignInManager.SignInAsync(UserToSignIn, ...).
public record RegisterOutcome(string? Error, string? Info, ApplicationUser? UserToSignIn = null);

// Thin wrapper over ASP.NET Identity so AuthController doesn't depend on it directly for anything
// but the actual cookie-writing calls (PasswordSignInAsync/SignInAsync/SignOutAsync), which only
// work from a real HTTP request/response and so stay in the Web host — see AuthController.
public interface IAuthService
{
    // Error is set on failure; Info is a non-error message on success (e.g. "check your email"),
    // set only when Identity:RequireConfirmedAccount means registering doesn't sign the user in.
    // referralCode is best-effort — an unknown/blank code never fails registration, see
    // IReferralService.RegisterReferralAsync.
    public Task<RegisterOutcome> RegisterAsync(RegisterRequest request, string name, string? referralCode = null);
    // Returns null on success, or a user-facing error message on failure.
    public Task<string?> ConfirmEmailAsync(string userId, string token);
    // Always succeeds silently — never reveals whether an account exists for the given email.
    public Task RequestPasswordResetAsync(string email);
    // Always succeeds silently — same non-disclosure as RequestPasswordResetAsync, and also a
    // silent no-op for an already-confirmed account (no reason to leak confirmation status either).
    public Task ResendConfirmationEmailAsync(string email);
    public Task<string?> ResetPasswordAsync(string email, string token, string newPassword);
    // Return null on success, or a user-facing error message on failure.
    // Resolves the caller's own account via ICurrentUser — only safe to call in-process
    // from a Razor component's DI scope (not from a plain HTTP controller action).
    public Task<string?> UpdateNameAsync(string newName);
    // Takes userId explicitly rather than via ICurrentUser — see the implementation's own
    // comment for why.
    public Task<string?> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
}
