using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;

namespace Netrom_Eco_Meal.Services.Interfaces;

public interface IAuthService
{
    public Task<SignInResult> LoginAsync(LoginRequest request);
    public Task<SignInResult> RegisterAsync(RegisterRequest request, string name);
    public Task LogoutAsync();
}