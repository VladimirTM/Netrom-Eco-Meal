using Microsoft.AspNetCore.Identity;
using Moq;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services;
using NetromEcoMeal.Services.Interfaces;
using NetromEcoMeal.Tests.TestSupport;

namespace NetromEcoMeal.Tests.Services;

// Covers GetBusinessWidgetStatsAsync — the one action here that's a real, anonymous, cross-origin
// HTTP endpoint (see ImpactController.GetBusinessWidgetAsync) rather than an in-process DI call,
// so it needs to fail closed for a business that shouldn't be publicly discoverable at all.
public class ImpactServiceTests
{
    private static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private sealed record Fixture(ImpactService Service, Mock<IOrderRepository> OrderRepo, Mock<IPackageRepository> PackageRepo, Mock<IBusinessRepository> BusinessRepo);

    private static Fixture Build()
    {
        var orderRepo = new Mock<IOrderRepository>();
        var packageRepo = new Mock<IPackageRepository>();
        var businessRepo = new Mock<IBusinessRepository>();
        var userManager = MockUserManager();
        var streakService = new Mock<IStreakService>();
        var currentUser = new FakeCurrentUser(null);

        var service = new ImpactService(orderRepo.Object, packageRepo.Object, businessRepo.Object, streakService.Object, userManager.Object, currentUser);
        return new Fixture(service, orderRepo, packageRepo, businessRepo);
    }

    private static Business Business(string status = BusinessStatuses.Approved, bool isHidden = false) => new()
    {
        Id = Guid.NewGuid(), Name = "Green Bites", Description = "d", Address = "a",
        BusinessTypeId = Guid.NewGuid(), Status = status, IsHidden = isHidden,
    };

    [Fact]
    public async Task GetBusinessWidgetStatsAsync_UnknownBusiness_ReturnsNull()
    {
        var f = Build();
        f.BusinessRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Business?)null);

        Assert.Null(await f.Service.GetBusinessWidgetStatsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetBusinessWidgetStatsAsync_HiddenBusiness_ReturnsNull()
    {
        var f = Build();
        var business = Business(isHidden: true);
        f.BusinessRepo.Setup(r => r.GetByIdAsync(business.Id)).ReturnsAsync(business);

        Assert.Null(await f.Service.GetBusinessWidgetStatsAsync(business.Id));
    }

    [Fact]
    public async Task GetBusinessWidgetStatsAsync_PendingApprovalBusiness_ReturnsNull()
    {
        var f = Build();
        var business = Business(status: BusinessStatuses.PendingApproval);
        f.BusinessRepo.Setup(r => r.GetByIdAsync(business.Id)).ReturnsAsync(business);

        Assert.Null(await f.Service.GetBusinessWidgetStatsAsync(business.Id));
    }

    [Fact]
    public async Task GetBusinessWidgetStatsAsync_Approved_CombinesOrderedAndDonatedWeightIntoEquivalencies()
    {
        var f = Build();
        var business = Business();
        f.BusinessRepo.Setup(r => r.GetByIdAsync(business.Id)).ReturnsAsync(business);
        f.OrderRepo.Setup(r => r.GetBusinessImpactStatsAsync(business.Id, It.IsAny<DateTime>())).ReturnsAsync((8m, 2m, 3));
        f.PackageRepo.Setup(r => r.GetDonatedWeightKgAsync(business.Id)).ReturnsAsync(2m);

        var stats = await f.Service.GetBusinessWidgetStatsAsync(business.Id);

        Assert.NotNull(stats);
        Assert.Equal(business.Name, stats.BusinessName);
        Assert.Equal(10m, stats.TotalKgSaved); // 8 ordered + 2 donated
        Assert.Equal(2m, stats.KgSavedThisMonth);
        Assert.Equal(3, stats.CompletedOrders);
        Assert.Equal(ImpactEquivalency.Co2eKg(10m), stats.Co2eKgAvoided);
        Assert.Equal(ImpactEquivalency.KmNotDriven(10m), stats.KmNotDriven);
        Assert.Equal(ImpactEquivalency.LitersOfWater(10m), stats.LitersWaterSaved);
    }
}
