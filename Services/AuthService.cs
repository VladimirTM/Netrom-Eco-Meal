using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

// Thin wrapper over ASP.NET Identity so AuthController doesn't depend on it directly.
public class AuthService(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager) : IAuthService
{
    public async Task<SignInResult> LoginAsync(LoginRequest request)
    {
        return await signInManager.PasswordSignInAsync(request.Email, request.Password, true, false);
    }

    public async Task<string?> RegisterAsync(RegisterRequest request, string name)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            return "An account with this email already exists.";
        }

        var user = new ApplicationUser
        {
            Name           = name,
            UserName       = request.Email,
            Email          = request.Email,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return string.Join(" ", result.Errors.Select(e => e.Description));
        }

        // Self-registration always starts as Customer; only an admin can promote from here.
        await userManager.AddToRoleAsync(user, AppRoles.Customer);

        var signInResult = await signInManager.PasswordSignInAsync(request.Email, request.Password, true, false);
        return signInResult.Succeeded ? null : "Account created, but automatic sign-in failed. Please log in.";
    }

    public async Task LogoutAsync()
    {
        await signInManager.SignOutAsync();
    }
}