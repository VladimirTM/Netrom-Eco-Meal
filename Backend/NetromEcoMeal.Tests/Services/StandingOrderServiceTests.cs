using Microsoft.Extensions.Configuration;
using Moq;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services;
using NetromEcoMeal.Services.Interfaces;
using NetromEcoMeal.Tests.TestSupport;

namespace NetromEcoMeal.Tests.Services;

public class StandingOrderServiceTests
{
    private const string UserId = "customer-1";
    private const string OtherUserId = "customer-2";
    private static readonly Guid BusinessId = Guid.NewGuid();
    private static readonly Guid PackageTypeId = Guid.NewGuid();
    private static readonly Guid OtherPackageTypeId = Guid.NewGuid();

    private sealed record Fixture(
        StandingOrderService Service,
        Mock<IStandingOrderRepository> StandingOrderRepo,
        Mock<IOrderRepository> OrderRepo,
        Mock<IBusinessService> BusinessService,
        Mock<INotificationService> NotificationService,
        Mock<IAppEmailSender> EmailSender);

    private static Fixture Build(string? userId, params string[] roles)
    {
        var standingOrderRepo = new Mock<IStandingOrderRepository>();
        var orderRepo = new Mock<IOrderRepository>();
        var businessService = new Mock<IBusinessService>();
        var notificationService = new Mock<INotificationService>();
        var emailSender = new Mock<IAppEmailSender>();
        var currentUser = new FakeCurrentUser(userId, roles);
        var configuration = new ConfigurationBuilder().Build();

        var service = new StandingOrderService(
            standingOrderRepo.Object, orderRepo.Object, businessService.Object,
            notificationService.Object, emailSender.Object, currentUser, configuration);

        return new Fixture(service, standingOrderRepo, orderRepo, businessService, notificationService, emailSender);
    }

    private static Business ApprovedBusiness() => new()
    {
        Id = BusinessId, Name = "Green Bites", Description = "d", Address = "a", BusinessTypeId = Guid.NewGuid(),
        Status = BusinessStatuses.Approved, IsHidden = false,
    };

    private static Package Package(Guid businessId, Guid packageTypeId, decimal price, params string[] tags) => new()
    {
        Id = Guid.NewGuid(), BusinessId = businessId, PackageTypeId = packageTypeId,
        Name = "Surprise Bag", Description = "d", Price = price, Quantity = 5, WeightKg = 1m,
        DietaryTags = [..tags], PickupStart = DateTime.UtcNow, PickupEnd = DateTime.UtcNow.AddHours(2),
    };

    private static StandingOrder Standing(Guid? packageTypeId = null, string? dietaryTag = null, decimal maxWeeklySpend = 50m, string userId = UserId) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, BusinessId = BusinessId, PackageTypeId = packageTypeId, DietaryTag = dietaryTag,
        MaxWeeklySpend = maxWeeklySpend, IsActive = true, CreatedAt = DateTime.UtcNow,
        User = new ApplicationUser { Id = userId, Name = "Customer", Email = "customer@example.com" },
    };

    // ---- CreateAsync ----------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Anonymous_Throws()
    {
        var f = Build(null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.CreateAsync(BusinessId, null, null, 20m));
    }

    [Fact]
    public async Task CreateAsync_NonCustomer_Throws()
    {
        var f = Build(UserId, AppRoles.BusinessManager);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.CreateAsync(BusinessId, null, null, 20m));
    }

    [Fact]
    public async Task CreateAsync_HiddenBusiness_Throws()
    {
        var f = Build(UserId, AppRoles.Customer);
        var hidden = ApprovedBusiness();
        hidden.IsHidden = true;
        f.BusinessService.Setup(b => b.GetByIdAsync(BusinessId)).ReturnsAsync(hidden);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.CreateAsync(BusinessId, null, null, 20m));
    }

    [Fact]
    public async Task CreateAsync_UnrecognizedDietaryTag_Throws()
    {
        var f = Build(UserId, AppRoles.Customer);
        f.BusinessService.Setup(b => b.GetByIdAsync(BusinessId)).ReturnsAsync(ApprovedBusiness());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.CreateAsync(BusinessId, null, "Keto", 20m));
    }

    [Fact]
    public async Task CreateAsync_Valid_PersistsAndClampsSpend()
    {
        var f = Build(UserId, AppRoles.Customer);
        f.BusinessService.Setup(b => b.GetByIdAsync(BusinessId)).ReturnsAsync(ApprovedBusiness());

        var created = await f.Service.CreateAsync(BusinessId, PackageTypeId, DietaryTags.GlutenFree, 999_999m);

        Assert.Equal(UserId, created.UserId);
        Assert.Equal(NetromEcoMeal.Constants.StandingOrders.MaxWeeklySpend, created.MaxWeeklySpend);
        f.StandingOrderRepo.Verify(r => r.AddAsync(It.IsAny<StandingOrder>()), Times.Once);
        f.StandingOrderRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ---- Update/Delete ownership ----------------------------------------------

    [Fact]
    public async Task UpdateAsync_NotOwner_Throws()
    {
        var f = Build(OtherUserId, AppRoles.Customer);
        var standing = Standing();
        f.StandingOrderRepo.Setup(r => r.GetByIdAsync(standing.Id)).ReturnsAsync(standing);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.UpdateAsync(standing.Id, 10m, false));
    }

    [Fact]
    public async Task UpdateAsync_Owner_UpdatesFields()
    {
        var f = Build(UserId, AppRoles.Customer);
        var standing = Standing();
        f.StandingOrderRepo.Setup(r => r.GetByIdAsync(standing.Id)).ReturnsAsync(standing);

        await f.Service.UpdateAsync(standing.Id, 15m, false);

        Assert.Equal(15m, standing.MaxWeeklySpend);
        Assert.False(standing.IsActive);
    }

    [Fact]
    public async Task DeleteAsync_NotOwner_Throws()
    {
        var f = Build(OtherUserId, AppRoles.Customer);
        var standing = Standing();
        f.StandingOrderRepo.Setup(r => r.GetByIdAsync(standing.Id)).ReturnsAsync(standing);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.DeleteAsync(standing.Id));
    }

    [Fact]
    public async Task DeleteAsync_Owner_Deletes()
    {
        var f = Build(UserId, AppRoles.Customer);
        var standing = Standing();
        f.StandingOrderRepo.Setup(r => r.GetByIdAsync(standing.Id)).ReturnsAsync(standing);

        await f.Service.DeleteAsync(standing.Id);

        f.StandingOrderRepo.Verify(r => r.DeleteAsync(standing.Id), Times.Once);
    }

    // ---- MatchNewPackagesAsync -------------------------------------------------

    [Fact]
    public async Task MatchNewPackagesAsync_NoActiveStandingOrders_NotifiesNobody()
    {
        var f = Build(null);
        f.StandingOrderRepo.Setup(r => r.GetActiveByBusinessIdAsync(BusinessId)).ReturnsAsync([]);

        var matched = await f.Service.MatchNewPackagesAsync([Package(BusinessId, PackageTypeId, 10m)]);

        Assert.Equal(0, matched);
        f.NotificationService.Verify(n => n.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task MatchNewPackagesAsync_PackageTypeMismatch_Skips()
    {
        var f = Build(null);
        var standing = Standing(packageTypeId: OtherPackageTypeId);
        f.StandingOrderRepo.Setup(r => r.GetActiveByBusinessIdAsync(BusinessId)).ReturnsAsync([standing]);
        f.BusinessService.Setup(b => b.GetByIdAsync(BusinessId)).ReturnsAsync(ApprovedBusiness());

        var matched = await f.Service.MatchNewPackagesAsync([Package(BusinessId, PackageTypeId, 10m)]);

        Assert.Equal(0, matched);
    }

    [Fact]
    public async Task MatchNewPackagesAsync_DietaryTagMismatch_Skips()
    {
        var f = Build(null);
        var standing = Standing(dietaryTag: DietaryTags.Vegan);
        f.StandingOrderRepo.Setup(r => r.GetActiveByBusinessIdAsync(BusinessId)).ReturnsAsync([standing]);
        f.BusinessService.Setup(b => b.GetByIdAsync(BusinessId)).ReturnsAsync(ApprovedBusiness());

        var matched = await f.Service.MatchNewPackagesAsync([Package(BusinessId, PackageTypeId, 10m, DietaryTags.Vegetarian)]);

        Assert.Equal(0, matched);
    }

    [Fact]
    public async Task MatchNewPackagesAsync_OverWeeklyBudget_Skips()
    {
        var f = Build(null);
        var standing = Standing(maxWeeklySpend: 15m);
        f.StandingOrderRepo.Setup(r => r.GetActiveByBusinessIdAsync(BusinessId)).ReturnsAsync([standing]);
        f.BusinessService.Setup(b => b.GetByIdAsync(BusinessId)).ReturnsAsync(ApprovedBusiness());
        f.OrderRepo.Setup(r => r.GetSpendInRangeAsync(UserId, BusinessId, null, null, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(10m);

        // Already spent 10 lei this week — a 10 lei package would push past the 15 lei cap.
        var matched = await f.Service.MatchNewPackagesAsync([Package(BusinessId, PackageTypeId, 10m)]);

        Assert.Equal(0, matched);
        f.NotificationService.Verify(n => n.CreateAsync(UserId, It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task MatchNewPackagesAsync_UnderWeeklyBudget_NotifiesAndEmails()
    {
        var f = Build(null);
        var standing = Standing(maxWeeklySpend: 30m);
        f.StandingOrderRepo.Setup(r => r.GetActiveByBusinessIdAsync(BusinessId)).ReturnsAsync([standing]);
        f.BusinessService.Setup(b => b.GetByIdAsync(BusinessId)).ReturnsAsync(ApprovedBusiness());
        f.OrderRepo.Setup(r => r.GetSpendInRangeAsync(UserId, BusinessId, null, null, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(5m);

        var package = Package(BusinessId, PackageTypeId, 10m);
        var matched = await f.Service.MatchNewPackagesAsync([package]);

        Assert.Equal(1, matched);
        f.NotificationService.Verify(n => n.CreateAsync(UserId, It.IsAny<string>(), $"/businesses/{BusinessId}?reserve={package.Id}"), Times.Once);
        f.EmailSender.Verify(e => e.SendEmailAsync("customer@example.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}
