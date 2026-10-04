using NetromEcoMeal.Constants;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Tests.TestSupport;

// Lets tests build an ICurrentUser around a specific signed-in user/roles (or anonymous) without
// needing a real HTTP request/JWT pipeline.
public class FakeCurrentUser(string? userId = null, params string[] roles) : ICurrentUser
{
    public Task<(bool IsAdmin, string? UserId)> GetCurrentUserAsync() =>
        Task.FromResult((userId is not null && roles.Contains(AppRoles.Admin), userId));

    public Task<bool> IsInRoleAsync(string role) => Task.FromResult(userId is not null && roles.Contains(role));
}
