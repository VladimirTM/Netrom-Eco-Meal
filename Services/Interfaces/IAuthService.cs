using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;

namespace Netrom_Eco_Meal.Services.Interfaces;

// Thin wrapper over ASP.NET Identity so AuthController doesn't depend on it directly.
public interface IAuthService
{
    public Task<SignInResult> LoginAsync(LoginRequest request);
    public Task<SignInResult> RegisterAsync(RegisterRequest request, string name);
    public Task LogoutAsync();
}