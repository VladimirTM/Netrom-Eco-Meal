using System.Net;
using System.Net.Http.Json;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Tests.TestSupport;

namespace NetromEcoMeal.Tests.Api;

// Its own AuthApiFixture instance (xUnit gives each test class its own copy of an IClassFixture<T>
// unless classes are grouped under a shared [Collection]) — the "auth" rate-limit bucket is
// per-app-instance state, and sharing an instance with AuthControllerTests' handful of logins
// would make the 11th-request assertion depend on test execution order.
[Collection(nameof(ApiCollection))]
public class AuthRateLimitTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    [Fact]
    public async Task EleventhLoginWithinAMinute_IsRateLimited()
    {
        var request = new LoginRequestDto("demo.customer@ecomeal.local", "WrongPassword!");

        for (var i = 0; i < 10; i++)
        {
            var response = await fixture.Client.PostAsJsonAsync("/api/auth/login", request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var eleventh = await fixture.Client.PostAsJsonAsync("/api/auth/login", request);
        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
    }
}
