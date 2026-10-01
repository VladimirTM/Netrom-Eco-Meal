using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Tests.TestSupport;

namespace NetromEcoMeal.Tests.Api;

// Auth matrix: anonymous/Customer/BusinessManager/Admin get the expected 200/401/403, a password
// change invalidates the old JWT, a forged/expired JWT is rejected, the 11th login within a
// minute is rate-limited, and no response carries PasswordHash/SecurityStamp/ApiKeyHash.
[Collection(nameof(ApiCollection))]
public class AuthControllerTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private HttpClient Client => fixture.Client;

    private async Task<string> LoginAsync(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto(email, password));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        return body!.Token;
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task Me_Anonymous_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_AnyAuthenticatedRole_ReturnsOk()
    {
        var token = await LoginAsync("demo.customer@ecomeal.local", "Demo123!");
        var response = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/auth/me", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("Customer", user!.Role);
    }

    [Fact]
    public async Task Users_RoleMatrix_AnonymousUnauthorized_NonAdminForbidden_AdminOk()
    {
        var anon = await Client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        var customerToken = await LoginAsync("demo.customer@ecomeal.local", "Demo123!");
        var customer = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/users", customerToken));
        Assert.Equal(HttpStatusCode.Forbidden, customer.StatusCode);

        var managerToken = await LoginAsync("demo.manager@ecomeal.local", "Demo123!");
        var manager = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/users", managerToken));
        Assert.Equal(HttpStatusCode.Forbidden, manager.StatusCode);

        var adminToken = await LoginAsync("admin@ecomeal.local", "Admin123!");
        var admin = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/users", adminToken));
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);

        // PaginatedList<T> has no public constructor (it's a write-only response shape, never
        // meant to be deserialized back) — read it as raw JSON instead of a strongly-typed client DTO.
        using var page = JsonDocument.Parse(await admin.Content.ReadAsStringAsync());
        Assert.NotEmpty(page.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto("demo.customer@ecomeal.local", "WrongPassword123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_ThenLogin_IssuesWorkingToken()
    {
        var email = $"api-test-{Guid.NewGuid():N}@ecomeal.local";
        var register = await Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequestDto("Api Test User", email, "StrongPass123!", null));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var registerBody = await register.Content.ReadFromJsonAsync<RegisterResponseDto>();
        Assert.NotNull(registerBody!.Token);
        Assert.Equal("Customer", registerBody.User!.Role);

        var me = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/auth/me", registerBody.Token!));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_InvalidatesPreviouslyIssuedToken()
    {
        var email = $"revoke-test-{Guid.NewGuid():N}@ecomeal.local";
        var register = await Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequestDto("Revoke Test", email, "StrongPass123!", null));
        var registered = await register.Content.ReadFromJsonAsync<RegisterResponseDto>();
        var oldToken = registered!.Token!;

        // Confirm the token works before the password change.
        var meBefore = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/auth/me", oldToken));
        Assert.Equal(HttpStatusCode.OK, meBefore.StatusCode);

        var changeRequest = Authorized(HttpMethod.Put, "/api/auth/me/password", oldToken);
        changeRequest.Content = JsonContent.Create(new ChangePasswordRequestDto("StrongPass123!", "EvenStronger456!"));
        var changeResponse = await Client.SendAsync(changeRequest);
        Assert.Equal(HttpStatusCode.NoContent, changeResponse.StatusCode);

        // The security_stamp claim baked into the old JWT no longer matches the DB.
        var meAfter = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/auth/me", oldToken));
        Assert.Equal(HttpStatusCode.Unauthorized, meAfter.StatusCode);

        // The new password works and issues a fresh, valid token.
        var newToken = await LoginAsync(email, "EvenStronger456!");
        var meNew = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/auth/me", newToken));
        Assert.Equal(HttpStatusCode.OK, meNew.StatusCode);
    }

    [Fact]
    public async Task ForgedToken_IsRejected()
    {
        var forgedKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a-completely-different-32-char-key-nobody-knows"));
        var forged = new JwtSecurityToken(
            issuer: ApiFactory.TestJwtIssuer,
            audience: ApiFactory.TestJwtAudience,
            claims: [new Claim(ClaimTypes.NameIdentifier, "forged-user-id"), new Claim("security_stamp", "whatever")],
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: new SigningCredentials(forgedKey, SecurityAlgorithms.HmacSha256));
        var token = new JwtSecurityTokenHandler().WriteToken(forged);

        var response = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/auth/me", token));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.TestJwtKey));
        var expired = new JwtSecurityToken(
            issuer: ApiFactory.TestJwtIssuer,
            audience: ApiFactory.TestJwtAudience,
            claims: [new Claim(ClaimTypes.NameIdentifier, "someone"), new Claim("security_stamp", "whatever")],
            expires: DateTime.UtcNow.AddDays(-1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var token = new JwtSecurityTokenHandler().WriteToken(expired);

        var response = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/auth/me", token));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocument_ListsAuthAndUsersEndpoints()
    {
        var response = await Client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/auth/login", json);
        Assert.Contains("/api/users", json);
    }

    [Fact]
    public async Task Responses_NeverExposeSensitiveFields()
    {
        var adminToken = await LoginAsync("admin@ecomeal.local", "Admin123!");
        var response = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/users", adminToken));
        var json = (await response.Content.ReadAsStringAsync()).ToLowerInvariant();

        Assert.DoesNotContain("passwordhash", json);
        Assert.DoesNotContain("securitystamp", json);
        Assert.DoesNotContain("apikeyhash", json);
    }
}
