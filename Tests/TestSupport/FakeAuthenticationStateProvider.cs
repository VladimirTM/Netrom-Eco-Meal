using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Netrom_Eco_Meal.Tests.TestSupport;

// Lets tests build a CurrentUserAccessor around a specific signed-in user/roles (or anonymous)
// without needing a real Blazor auth pipeline.
public class FakeAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly AuthenticationState _state;

    public FakeAuthenticationStateProvider(string? userId = null, params string[] roles)
    {
        if (userId is null)
        {
            _state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            return;
        }

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, "Test");
        _state = new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);
}
