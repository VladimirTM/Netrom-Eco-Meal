using Moq;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// LoyaltyService is deliberately stateless — every call recomputes progress/discount straight
// from a mocked GetCompletedOrderCountAsync rather than a stored counter, so these tests are
// really about the threshold/remainder math and the "both fields must be set" guard.
public class LoyaltyServiceTests
{
    private const string UserId = "customer-1";
    private static readonly Guid BusinessId = Guid.NewGuid();

    private sealed record Fixture(LoyaltyService Service, Mock<IOrderRepository> OrderRepo, Mock<IBusinessService> BusinessService);

    private static Fixture Build(string? userId, Business? business)
    {
        var orderRepo = new Mock<IOrderRepository>();
        var businessService = new Mock<IBusinessService>();
        businessService.Setup(b => b.GetByIdAsync(BusinessId)).ReturnsAsync(business);
        var currentUser = new CurrentUserAccessor(new FakeAuthenticationStateProvider(userId));
        var service = new LoyaltyService(orderRepo.Object, businessService.Object, currentUser);
        return new Fixture(service, orderRepo, businessService);
    }

    private static Business BusinessWithPunchCard(int threshold, decimal discount) => new()
    {
        Id = BusinessId,
        Name = "Green Bites",
        Description = "d",
        Address = "a",
        BusinessTypeId = Guid.NewGuid(),
        LoyaltyPunchThreshold = threshold,
        LoyaltyDiscountAmount = discount,
    };

    // ---- GetMyProgressAsync --------------------------------------------------

    [Fact]
    public async Task GetMyProgressAsync_Anonymous_ReturnsNull()
    {
        var f = Build(null, BusinessWithPunchCard(6, 2m));

        Assert.Null(await f.Service.GetMyProgressAsync(BusinessId));
    }

    [Fact]
    public async Task GetMyProgressAsync_NoPunchCardConfigured_ReturnsNull()
    {
        var business = new Business { Id = BusinessId, Name = "n", Description = "d", Address = "a", BusinessTypeId = Guid.NewGuid() };
        var f = Build(UserId, business);

        Assert.Null(await f.Service.GetMyProgressAsync(BusinessId));
    }

    [Fact]
    public async Task GetMyProgressAsync_MidwayThroughThreshold_ReportsOrdersRemaining()
    {
        var f = Build(UserId, BusinessWithPunchCard(6, 2m));
        f.OrderRepo.Setup(r => r.GetCompletedOrderCountAsync(UserId, BusinessId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(4);

        var progress = await f.Service.GetMyProgressAsync(BusinessId);

        Assert.NotNull(progress);
        Assert.Equal(6, progress!.Threshold);
        Assert.Equal(2m, progress.DiscountAmount);
        Assert.Equal(4, progress.CompletedThisMonth);
        Assert.Equal(2, progress.OrdersUntilReward);
    }

    [Fact]
    public async Task GetMyProgressAsync_JustHitThreshold_NextRewardIsFullThresholdAway()
    {
        var f = Build(UserId, BusinessWithPunchCard(6, 2m));
        f.OrderRepo.Setup(r => r.GetCompletedOrderCountAsync(UserId, BusinessId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(6);

        var progress = await f.Service.GetMyProgressAsync(BusinessId);

        Assert.Equal(6, progress!.OrdersUntilReward);
    }

    // ---- EvaluateDiscountAsync -----------------------------------------------

    [Fact]
    public async Task EvaluateDiscountAsync_NoPunchCardConfigured_ReturnsNull()
    {
        var business = new Business { Id = BusinessId, Name = "n", Description = "d", Address = "a", BusinessTypeId = Guid.NewGuid() };
        var f = Build(UserId, business);

        Assert.Null(await f.Service.EvaluateDiscountAsync(UserId, BusinessId));
    }

    [Fact]
    public async Task EvaluateDiscountAsync_NextOrderWouldBeTheNth_ReturnsDiscount()
    {
        var f = Build(UserId, BusinessWithPunchCard(6, 2m));
        // 5 completed so far — this checkout's order would become the 6th.
        f.OrderRepo.Setup(r => r.GetCompletedOrderCountAsync(UserId, BusinessId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(5);

        Assert.Equal(2m, await f.Service.EvaluateDiscountAsync(UserId, BusinessId));
    }

    [Fact]
    public async Task EvaluateDiscountAsync_NextOrderNotOnThreshold_ReturnsNull()
    {
        var f = Build(UserId, BusinessWithPunchCard(6, 2m));
        f.OrderRepo.Setup(r => r.GetCompletedOrderCountAsync(UserId, BusinessId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(3);

        Assert.Null(await f.Service.EvaluateDiscountAsync(UserId, BusinessId));
    }
}
