using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Netrom_Eco_Meal.Constants;

namespace Netrom_Eco_Meal.Services;

public class CurrentUserAccessor(AuthenticationStateProvider authenticationStateProvider)
{
    public async Task<(bool IsAdmin, string? UserId)> GetCurrentUserAsync()
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var isAdmin = authState.User.IsInRole(AppRoles.Admin);
        var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return (isAdmin, userId);
    }
}