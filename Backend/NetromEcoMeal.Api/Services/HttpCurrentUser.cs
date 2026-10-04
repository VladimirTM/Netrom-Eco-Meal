using System.Security.Claims;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Services;

// ICurrentUser for the Api host: reads the JWT-derived ClaimsPrincipal off the current request.
public class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Task<(bool IsAdmin, string? UserId)> GetCurrentUserAsync()
    {
        var isAdmin = User?.IsInRole(AppRoles.Admin) ?? false;
        var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Task.FromResult((isAdmin, userId));
    }

    public Task<bool> IsInRoleAsync(string role) => Task.FromResult(User?.IsInRole(role) ?? false);
}
