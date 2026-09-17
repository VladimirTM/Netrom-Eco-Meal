using Moq;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// StreakService is deliberately stateless — every call recomputes the streak straight from a
// mocked GetCompletedOrderCreatedDatesAsync rather than a stored counter, so these tests are
// really about the "this week doesn't break it, a fully-elapsed empty week does" week-walking math.
public class StreakServiceTests
{
    private const string UserId = "customer-1";

    private static Mock<IOrderRepository> MockRepo(params DateTime[] completedDates)
    {
        var repo = new Mock<IOrderRepository>();
        repo.Setup(r => r.GetCompletedOrderCreatedDatesAsync(UserId)).ReturnsAsync(completedDates.ToList());
        return repo;
    }

    private static StreakService Build(Mock<IOrderRepository> repo) =>
        new(repo.Object, new CurrentUserAccessor(new FakeAuthenticationStateProvider(UserId)));

    [Fact]
    public async Task GetStreakWeeksAsync_NoCompletedOrders_ReturnsZero()
    {
        var service = Build(MockRepo());

        Assert.Equal(0, await service.GetStreakWeeksAsync(UserId));
    }

    [Fact]
    public async Task GetStreakWeeksAsync_OrderEveryWeekForFourWeeks_ReturnsFour()
    {
        var now = DateTime.UtcNow;
        var repo = MockRepo(now, now.AddDays(-7), now.AddDays(-14), now.AddDays(-21));

        Assert.Equal(4, await Build(repo).GetStreakWeeksAsync(UserId));
    }

    [Fact]
    public async Task GetStreakWeeksAsync_ThisWeekHasNoOrderYet_StillCountsPriorConsecutiveWeeks()
    {
        // Nothing this week, but last week and the week before both have one — an in-progress
        // week shouldn't zero out an otherwise-live streak.
        var now = DateTime.UtcNow;
        var repo = MockRepo(now.AddDays(-7), now.AddDays(-14));

        Assert.Equal(2, await Build(repo).GetStreakWeeksAsync(UserId));
    }

    [Fact]
    public async Task GetStreakWeeksAsync_GapTwoWeeksAgo_StopsCountingAtTheGap()
    {
        // This week and last week both have orders, but 2 weeks ago is empty — the streak should
        // stop there even though 3 weeks ago has an order too.
        var now = DateTime.UtcNow;
        var repo = MockRepo(now, now.AddDays(-7), now.AddDays(-21));

        Assert.Equal(2, await Build(repo).GetStreakWeeksAsync(UserId));
    }

    [Fact]
    public async Task GetStreakWeeksAsync_LastOrderMoreThanAWeekAgo_ReturnsZero()
    {
        var now = DateTime.UtcNow;
        var repo = MockRepo(now.AddDays(-15));

        Assert.Equal(0, await Build(repo).GetStreakWeeksAsync(UserId));
    }

    [Fact]
    public async Task GetMyStreakWeeksAsync_Anonymous_ReturnsZero()
    {
        var repo = new Mock<IOrderRepository>();
        var service = new StreakService(repo.Object, new CurrentUserAccessor(new FakeAuthenticationStateProvider(null)));

        Assert.Equal(0, await service.GetMyStreakWeeksAsync());
        repo.Verify(r => r.GetCompletedOrderCreatedDatesAsync(It.IsAny<string>()), Times.Never);
    }
}
