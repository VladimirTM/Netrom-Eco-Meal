namespace NetromEcoMeal.Services.Interfaces;

// Lets services read the caller's identity/role without depending on how that identity was
// resolved. CircuitCurrentUser (Web) wraps AuthenticationStateProvider for Blazor pages;
// HttpCurrentUser (Api, Phase 3) will read IHttpContextAccessor.HttpContext.User instead.
public interface ICurrentUser
{
    public Task<(bool IsAdmin, string? UserId)> GetCurrentUserAsync();

    public Task<bool> IsInRoleAsync(string role);

    // Was copy-pasted as a private method across BusinessService, ReportService, UserService, and
    // inline in AuditLogService — centralized here so a future change to the admin check (e.g.
    // logging denied attempts) only has one place to land. A default method so every
    // implementation (circuit-based, HTTP-based, fake) gets it for free from GetCurrentUserAsync.
    public async Task EnsureAdminAsync(string message = "Only an admin can perform this action.")
    {
        var (isAdmin, _) = await GetCurrentUserAsync();
        if (!isAdmin)
            throw new UnauthorizedAccessException(message);
    }
}
