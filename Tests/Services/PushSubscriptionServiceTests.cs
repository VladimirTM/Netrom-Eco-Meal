using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// Covers PushSubscriptionService's authorization (Subscribe/Unsubscribe scoped to the signed-in
// user, mirroring FavoriteService) and SendToUserAsync's fan-out + prune-on-410 behavior.
// IPushSubscriptionRepository/IWebPushGateway are mocked; the repository's own persistence is
// exercised indirectly through the mock's Verify calls rather than a real DbContext.
public class PushSubscriptionServiceTests
{
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";

    private sealed record Fixture(PushSubscriptionService Service, Mock<IPushSubscriptionRepository> Repo, Mock<IWebPushGateway> Gateway);

    private static Fixture Build(string? userId)
    {
        var repo = new Mock<IPushSubscriptionRepository>();
        var gateway = new Mock<IWebPushGateway>();
        var currentUser = new CurrentUserAccessor(new FakeAuthenticationStateProvider(userId));
        var service = new PushSubscriptionService(repo.Object, gateway.Object, currentUser, NullLogger<PushSubscriptionService>.Instance);
        return new Fixture(service, repo, gateway);
    }

    private static Entities.PushSubscription Subscription(string userId, string endpoint) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Endpoint = endpoint,
        P256Dh = "p256dh-key",
        Auth = "auth-key",
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task SubscribeAsync_Anonymous_Throws()
    {
        var f = Build(null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.SubscribeAsync("https://push.example/ep", "p256dh", "auth"));
    }

    [Fact]
    public async Task SubscribeAsync_SignedIn_DelegatesToRepository()
    {
        var f = Build(UserId);

        await f.Service.SubscribeAsync("https://push.example/ep", "p256dh", "auth");

        f.Repo.Verify(r => r.AddAsync(UserId, "https://push.example/ep", "p256dh", "auth"), Times.Once);
    }

    [Fact]
    public async Task UnsubscribeAsync_Anonymous_DoesNothing()
    {
        var f = Build(null);

        await f.Service.UnsubscribeAsync("https://push.example/ep");

        f.Repo.Verify(r => r.RemoveByEndpointAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UnsubscribeAsync_EndpointNotOwnedByCaller_DoesNotRemove()
    {
        var f = Build(UserId);
        f.Repo.Setup(r => r.GetByUserIdAsync(UserId)).ReturnsAsync([Subscription(UserId, "https://push.example/mine")]);

        await f.Service.UnsubscribeAsync("https://push.example/someone-elses");

        f.Repo.Verify(r => r.RemoveByEndpointAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UnsubscribeAsync_EndpointOwnedByCaller_Removes()
    {
        var f = Build(UserId);
        f.Repo.Setup(r => r.GetByUserIdAsync(UserId)).ReturnsAsync([Subscription(UserId, "https://push.example/mine")]);

        await f.Service.UnsubscribeAsync("https://push.example/mine");

        f.Repo.Verify(r => r.RemoveByEndpointAsync("https://push.example/mine"), Times.Once);
    }

    [Fact]
    public void GetPublicKey_GatewayNotConfigured_ReturnsNull()
    {
        var f = Build(UserId);
        f.Gateway.Setup(g => g.IsConfigured).Returns(false);

        Assert.Null(f.Service.GetPublicKey());
    }

    [Fact]
    public void GetPublicKey_GatewayConfigured_ReturnsKey()
    {
        var f = Build(UserId);
        f.Gateway.Setup(g => g.IsConfigured).Returns(true);
        f.Gateway.Setup(g => g.PublicKey).Returns("public-key");

        Assert.Equal("public-key", f.Service.GetPublicKey());
    }

    [Fact]
    public async Task SendToUserAsync_GatewayNotConfigured_NeverLoadsSubscriptions()
    {
        var f = Build(UserId);
        f.Gateway.Setup(g => g.IsConfigured).Returns(false);

        await f.Service.SendToUserAsync(OtherUserId, "message", "/orders");

        f.Repo.Verify(r => r.GetByUserIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendToUserAsync_SendsToEverySubscription_AndPrunesOnesTheGatewayReportsGone()
    {
        var f = Build(UserId);
        var stillValid = Subscription(OtherUserId, "https://push.example/still-valid");
        var gone = Subscription(OtherUserId, "https://push.example/gone");
        f.Gateway.Setup(g => g.IsConfigured).Returns(true);
        f.Repo.Setup(r => r.GetByUserIdAsync(OtherUserId)).ReturnsAsync([stillValid, gone]);
        f.Gateway.Setup(g => g.SendAsync(stillValid, "Eco Meal", "message", "/orders")).ReturnsAsync(true);
        f.Gateway.Setup(g => g.SendAsync(gone, "Eco Meal", "message", "/orders")).ReturnsAsync(false);

        await f.Service.SendToUserAsync(OtherUserId, "message", "/orders");

        f.Repo.Verify(r => r.RemoveByEndpointAsync("https://push.example/gone"), Times.Once);
        f.Repo.Verify(r => r.RemoveByEndpointAsync("https://push.example/still-valid"), Times.Never);
    }
}
