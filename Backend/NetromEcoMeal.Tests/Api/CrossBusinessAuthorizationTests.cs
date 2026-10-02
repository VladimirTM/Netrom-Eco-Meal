using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Tests.TestSupport;

namespace NetromEcoMeal.Tests.Api;

// A manager of business A gets 403 on business B's packages and orders. DbSeeder staffs
// demo.manager at both Stadionul de Gusturi (A, id ...0001) and VAR Bistro (B, id ...0002), but
// demo.manager2 only at Stadionul, so demo.manager2 reaching for VAR Bistro is the cross-business case.
[Collection(nameof(ApiCollection))]
public class CrossBusinessAuthorizationTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private static readonly Guid StadionulDeGusturiId = new("44444444-0000-0000-0000-000000000001");
    private static readonly Guid VarBistroId = new("44444444-0000-0000-0000-000000000002");

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
    public async Task Manager2_PackageAnalytics_OwnBusiness_Ok_OtherManagersBusiness_Forbidden()
    {
        var token = await LoginAsync("demo.manager2@ecomeal.local", "Demo123!");
        var since = DateTime.UtcNow.AddDays(-30).ToString("O");

        var ownBusiness = await Client.SendAsync(Authorized(HttpMethod.Get,
            $"/api/packages/analytics?businessId={StadionulDeGusturiId}&since={since}", token));
        Assert.Equal(HttpStatusCode.OK, ownBusiness.StatusCode);

        var otherManagersBusiness = await Client.SendAsync(Authorized(HttpMethod.Get,
            $"/api/packages/analytics?businessId={VarBistroId}&since={since}", token));
        Assert.Equal(HttpStatusCode.Forbidden, otherManagersBusiness.StatusCode);
    }

    [Fact]
    public async Task Manager2_OrdersForManagement_OwnBusiness_Ok_OtherManagersBusiness_Forbidden()
    {
        var token = await LoginAsync("demo.manager2@ecomeal.local", "Demo123!");

        var ownBusiness = await Client.SendAsync(Authorized(HttpMethod.Get,
            $"/api/orders/manage?businessId={StadionulDeGusturiId}", token));
        Assert.Equal(HttpStatusCode.OK, ownBusiness.StatusCode);

        var otherManagersBusiness = await Client.SendAsync(Authorized(HttpMethod.Get,
            $"/api/orders/manage?businessId={VarBistroId}", token));
        Assert.Equal(HttpStatusCode.Forbidden, otherManagersBusiness.StatusCode);
    }

    [Fact]
    public async Task Manager_StaffingBothBusinesses_GetsOkForEither()
    {
        // Sanity check that the 403 above is about staffing, not about VAR Bistro's id specifically —
        // demo.manager (staff of both businesses) gets 200 for the same business demo.manager2 was
        // forbidden from.
        var token = await LoginAsync("demo.manager@ecomeal.local", "Demo123!");

        var response = await Client.SendAsync(Authorized(HttpMethod.Get, $"/api/orders/manage?businessId={VarBistroId}", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Responses_NeverExposeSensitiveFields_AcrossCatalogAndOrders()
    {
        var adminToken = await LoginAsync("admin@ecomeal.local", "Admin123!");

        var businesses = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/businesses/all", adminToken));
        var packages = await Client.GetAsync("/api/packages");
        var ordersForManagement = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/orders/manage", adminToken));

        foreach (var response in new[] { businesses, packages, ordersForManagement })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = (await response.Content.ReadAsStringAsync()).ToLowerInvariant();
            Assert.DoesNotContain("passwordhash", json);
            Assert.DoesNotContain("securitystamp", json);
            Assert.DoesNotContain("apikeyhash", json);
        }
    }

    [Fact]
    public async Task OpenApiDocument_ListsEveryPhase3Batch()
    {
        var response = await Client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        foreach (var path in new[]
                 {
                     "/api/businesses", "/api/business-types", "/api/package-types", "/api/brands",
                     "/api/packages", "/api/package-templates", "/api/reviews", "/api/kitchen-tips",
                     "/api/favorites", "/api/uploads/{subfolder}",
                     "/api/orders", "/api/payments/checkout-session", "/api/rescue-circles", "/api/orders/export",
                     "/api/notifications/mine", "/api/push-subscriptions", "/api/loyalty/{businessId}/mine",
                     "/api/standing-orders", "/api/referrals/mine", "/api/streaks/mine", "/api/impact/leaderboard",
                     "/api/reports", "/api/audit-log",
                     "/api/ai/search-intent", "/api/ai/basket-plan", "/api/ai/draft-description",
                     "/api/webhooks/packages",
                 })
            Assert.Contains(path, json);
    }
}
