namespace NetromEcoMeal.DTOs;

// Never hand ApplicationUser straight to JSON — it carries PasswordHash/SecurityStamp.
public record UserDto(string Id, string Name, string Email, string Role);

public record AuthResponseDto(string Token, UserDto User);

public record LoginRequestDto(string Email, string Password);

public record RegisterRequestDto(string Name, string Email, string Password, string? ReferralCode);

// Info is set instead of Token when Identity:RequireConfirmedAccount means registering doesn't
// sign the caller in yet — the frontend shows it and offers "resend confirmation".
public record RegisterResponseDto(string? Token, UserDto? User, string? Info);

public record ChangePasswordRequestDto(string CurrentPassword, string NewPassword);

public record UpdateNameRequestDto(string Name);

public record ForgotPasswordRequestDto(string Email);

public record ResendConfirmationRequestDto(string Email);

public record ResetPasswordRequestDto(string Email, string Token, string NewPassword);

public record ConfirmEmailRequestDto(string UserId, string Token);

// Code lets the frontend branch on a specific failure (e.g. "email_not_confirmed" → offer
// "resend confirmation") without string-matching Error, which is meant for display only.
public record AuthErrorDto(string Error, string Code);
