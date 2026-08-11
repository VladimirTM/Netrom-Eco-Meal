using Moq;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// Covers the one non-obvious rule CreateAsync adds beyond "insert a row": every bell notification
// also fires a best-effort push via IPushSubscriptionService, centralized here rather than at each
// of OrderService/PackageService/BusinessService's call sites — see NotificationService.CreateAsync.
public class NotificationServiceTests
{
    private sealed record Fixture(NotificationService Service, Mock<INotificationRepository> Repo, Mock<IPushSubscriptionService> Push);

    private static Fixture Build(string? userId = null)
    {
        var repo = new Mock<INotificationRepository>();
        var push = new Mock<IPushSubscriptionService>();
        var currentUser = new CurrentUserAccessor(new FakeAuthenticationStateProvider(userId));
        var service = new NotificationService(repo.Object, push.Object, currentUser);
        return new Fixture(service, repo, push);
    }

    [Fact]
    public async Task CreateAsync_InsertsNotification_AndFiresPushToTheTargetUser()
    {
        var f = Build();

        await f.Service.CreateAsync("target-user", "Your order was confirmed", "/orders");

        f.Repo.Verify(r => r.CreateAsync(It.Is<Notification>(n =>
            n.UserId == "target-user" && n.Message == "Your order was confirmed" && n.Url == "/orders")), Times.Once);
        f.Push.Verify(p => p.SendToUserAsync("target-user", "Your order was confirmed", "/orders"), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_NoUrl_PassesNullUrlToPushToo()
    {
        var f = Build();

        await f.Service.CreateAsync("target-user", "Your application wasn't approved");

        f.Push.Verify(p => p.SendToUserAsync("target-user", "Your application wasn't approved", null), Times.Once);
    }
}
