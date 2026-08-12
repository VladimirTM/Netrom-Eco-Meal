using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Netrom_Eco_Meal.Constants;

namespace Netrom_Eco_Meal.Services;

// Lets services read the caller's identity/role without depending on HttpContext, which
// isn't reliably available in Blazor Server's persistent-connection model.
public class CurrentUserAccessor(AuthenticationStateProvider authenticationStateProvider)
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

    // Was copy-pasted as a private method across BusinessService, ReportService, UserService, and
    // inline in AuditLogService — centralized here so a future change to the admin check (e.g.
    // logging denied attempts) only has one place to land.
    public async Task EnsureAdminAsync(string message = "Only an admin can perform this action.")
    {
        var (isAdmin, _) = await GetCurrentUserAsync();
        if (!isAdmin)
            throw new UnauthorizedAccessException(message);
    }
}