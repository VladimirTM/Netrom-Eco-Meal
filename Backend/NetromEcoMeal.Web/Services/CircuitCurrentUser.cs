using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Services;

// ICurrentUser for Blazor Server: reads the circuit's AuthenticationStateProvider, which is
// reliably available inside a circuit even when HttpContext isn't (see BACKEND_ARCHITECTURE.md).
public class CircuitCurrentUser(AuthenticationStateProvider authenticationStateProvider) : ICurrentUser
{
    public async Task<(bool IsAdmin, string? UserId)> GetCurrentUserAsync()
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var isAdmin = authState.User.IsInRole(AppRoles.Admin);
        var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return (isAdmin, userId);
    }

    public async Task<bool> IsInRoleAsync(string role)
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        return authState.User.IsInRole(role);
    }
}
