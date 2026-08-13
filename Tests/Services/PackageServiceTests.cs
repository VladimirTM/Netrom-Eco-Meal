using Microsoft.Extensions.Configuration;
using Moq;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// Covers PackageService's Phase 8 additions: the /packages bulk-action toolbar (duplicate,
// adjust quantity, extend pickup window) and the Dashboard business-analytics query. Both share
// the same "admin, or staff of every affected package's business" authorization as the existing
// single-package write methods. IPackageRepository/IBusinessService are mocked.
public class PackageServiceTests
{
    private const string AdminId = "admin-1";
    private const string ManagerId = "manager-1";
    private const string OtherManagerId = "manager-2";

    private sealed record Fixture(PackageService Service, Mock<IPackageRepository> Repo, Mock<IBusinessService> BusinessService, Mock<INotificationService> NotificationService, PackageStockBroadcaster StockBroadcaster);

    private static Fixture Build(string? userId, params string[] roles)
    {
        var repo = new Mock<IPackageRepository>();
        var businessService = new Mock<IBusinessService>();
        var favoriteRepo = new Mock<IFavoriteRepository>();
        var notificationService = new Mock<INotificationService>();
        var emailSender = new Mock<IAppEmailSender>();
        var auditLogService = new Mock<IAuditLogService>();
        var currentUser = new CurrentUserAccessor(new FakeAuthenticationStateProvider(userId, roles));
        var configuration = new ConfigurationBuilder().Build();
        var stockBroadcaster = new PackageStockBroadcaster();

        // Default so HideAsync's staff-notification fan-out (NotifyHiddenAsync) has something to
        // enumerate; tests that care about notification content override this per-case.
        businessService.Setup(b => b.GetStaffAsync(It.IsAny<Guid>())).ReturnsAsync([]);

        var service = new PackageService(
            repo.Object, businessService.Object, favoriteRepo.Object, notificationService.Object,
            emailSender.Object, currentUser, configuration, auditLogService.Object, stockBroadcaster);

        return new Fixture(service, repo, businessService, notificationService, stockBroadcaster);
    }

    // ---- DuplicateManyAsync -------------------------------------------------

    [Fact]
    public async Task DuplicateManyAsync_ManagerNotStaffOfBusiness_Throws()
    {
        var f = Build(ManagerId, AppRoles.BusinessManager);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        f.Repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([package]);
        f.BusinessService.Setup(b => b.IsStaffAsync(businessId, ManagerId)).ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.DuplicateManyAsync([package.Id]));
    }

    [Fact]
    public async Task DuplicateManyAsync_Success_CreatesIndependentCopiesWithNewIds()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 4);
        package.TemplateId = Guid.NewGuid();
        f.Repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([package]);

        var duplicates = await f.Service.DuplicateManyAsync([package.Id]);

        var duplicate = Assert.Single(duplicates);
        Assert.NotEqual(package.Id, duplicate.Id);
        Assert.Equal(package.Name, duplicate.Name);
        Assert.Equal(package.Quantity, duplicate.Quantity);
        Assert.Equal(package.BusinessId, duplicate.BusinessId);
        // A duplicate is a standalone package, not another instance of the original's template.
        Assert.Null(duplicate.TemplateId);
        f.Repo.Verify(r => r.AddAsync(duplicate), Times.Once);
        f.Repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // Regression test: a moderator-hidden package must not resurface via its duplicate.
    [Fact]
    public async Task DuplicateManyAsync_SourceIsHidden_DuplicateStaysHidden()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        package.IsHidden = true;
        package.HiddenReason = "Mislabeled allergens";
        f.Repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([package]);

        var duplicates = await f.Service.DuplicateManyAsync([package.Id]);

        var duplicate = Assert.Single(duplicates);
        Assert.True(duplicate.IsHidden);
        Assert.Equal("Mislabeled allergens", duplicate.HiddenReason);
    }

    // ---- AdjustQuantityManyAsync ---------------------------------------------

    [Fact]
    public async Task AdjustQuantityManyAsync_PositiveDelta_IncreasesEachSelectedPackage()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var businessId = Guid.NewGuid();
        var packages = new List<Package> { TestData.Package(businessId, quantity: 2), TestData.Package(businessId, quantity: 5) };
        f.Repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync(packages);

        await f.Service.AdjustQuantityManyAsync(packages.Select(p => p.Id).ToList(), 3);

        Assert.Equal(5, packages[0].Quantity);
        Assert.Equal(8, packages[1].Quantity);
        f.Repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AdjustQuantityManyAsync_NegativeDelta_ClampsAtZero()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 2);
        f.Repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([package]);

        await f.Service.AdjustQuantityManyAsync([package.Id], -10);

        Assert.Equal(0, package.Quantity);
    }

    [Fact]
    public async Task AdjustQuantityManyAsync_ManagerNotStaffOfOneOfTwoBusinesses_ThrowsAndSavesNothing()
    {
        var f = Build(ManagerId, AppRoles.BusinessManager);
        var ownBusinessId = Guid.NewGuid();
        var otherBusinessId = Guid.NewGuid();
        var owned = TestData.Package(ownBusinessId, quantity: 2);
        var notOwned = TestData.Package(otherBusinessId, quantity: 2);
        f.Repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([owned, notOwned]);
        f.BusinessService.Setup(b => b.IsStaffAsync(ownBusinessId, ManagerId)).ReturnsAsync(true);
        f.BusinessService.Setup(b => b.IsStaffAsync(otherBusinessId, ManagerId)).ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.AdjustQuantityManyAsync([owned.Id, notOwned.Id], 5));

        f.Repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    // ---- ExtendPickupWindowManyAsync ------------------------------------------

    [Fact]
    public async Task ExtendPickupWindowManyAsync_ExtendsEndOnly()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        var originalStart = package.PickupStart;
        var originalEnd = package.PickupEnd;
        f.Repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([package]);

        await f.Service.ExtendPickupWindowManyAsync([package.Id], TimeSpan.FromHours(2));

        Assert.Equal(originalStart, package.PickupStart);
        Assert.Equal(originalEnd.AddHours(2), package.PickupEnd);
        f.Repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ---- GetForAnalyticsAsync -------------------------------------------------

    [Fact]
    public async Task GetForAnalyticsAsync_ManagerWithoutBusinessId_Throws()
    {
        var f = Build(ManagerId, AppRoles.BusinessManager);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.GetForAnalyticsAsync(null, DateTime.UtcNow.AddDays(-13)));
    }

    [Fact]
    public async Task GetForAnalyticsAsync_ManagerNotStaffOfRequestedBusiness_Throws()
    {
        var f = Build(ManagerId, AppRoles.BusinessManager);
        var businessId = Guid.NewGuid();
        f.BusinessService.Setup(b => b.IsStaffAsync(businessId, ManagerId)).ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.GetForAnalyticsAsync(businessId, DateTime.UtcNow.AddDays(-13)));
    }

    [Fact]
    public async Task GetForAnalyticsAsync_ManagerStaffOfBusiness_DelegatesToRepository()
    {
        var f = Build(ManagerId, AppRoles.BusinessManager);
        var businessId = Guid.NewGuid();
        var since = DateTime.UtcNow.AddDays(-13);
        f.BusinessService.Setup(b => b.IsStaffAsync(businessId, ManagerId)).ReturnsAsync(true);
        f.Repo.Setup(r => r.GetForAnalyticsAsync(businessId, since)).ReturnsAsync([TestData.Package(businessId)]);

        var result = await f.Service.GetForAnalyticsAsync(businessId, since);

        Assert.Single(result);
    }

    [Fact]
    public async Task GetForAnalyticsAsync_Admin_AllowsNullBusinessId()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var since = DateTime.UtcNow.AddDays(-13);
        f.Repo.Setup(r => r.GetForAnalyticsAsync(null, since)).ReturnsAsync([]);

        var result = await f.Service.GetForAnalyticsAsync(null, since);

        Assert.Empty(result);
        f.Repo.Verify(r => r.GetForAnalyticsAsync(null, since), Times.Once);
    }

    // ---- HideAsync / UnhideAsync -------------------------------------------

    [Fact]
    public async Task HideAsync_ManagerNotStaffOfBusiness_Throws()
    {
        var f = Build(ManagerId, AppRoles.BusinessManager);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        f.Repo.Setup(r => r.GetByIdAsync(package.Id)).ReturnsAsync(package);
        f.BusinessService.Setup(b => b.IsStaffAsync(businessId, ManagerId)).ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.HideAsync(package.Id, "reason"));
    }

    [Fact]
    public async Task HideAsync_Admin_SetsHiddenWithReason()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        f.Repo.Setup(r => r.GetByIdAsync(package.Id)).ReturnsAsync(package);

        await f.Service.HideAsync(package.Id, "Allergen mislabeled");

        Assert.True(package.IsHidden);
        Assert.Equal("Allergen mislabeled", package.HiddenReason);
        f.Repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // Regression test: hiding a package should notify its business's staff, the same way
    // BusinessService.HideAsync already notifies staff when the business itself is hidden.
    [Fact]
    public async Task HideAsync_Admin_NotifiesBusinessStaff()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        var staffMember = TestData.User(ManagerId);
        f.Repo.Setup(r => r.GetByIdAsync(package.Id)).ReturnsAsync(package);
        f.BusinessService.Setup(b => b.GetStaffAsync(businessId)).ReturnsAsync([staffMember]);

        await f.Service.HideAsync(package.Id, "Allergen mislabeled");

        f.NotificationService.Verify(n => n.CreateAsync(ManagerId, It.Is<string>(m => m.Contains(package.Name)), null), Times.Once);
    }

    // HideAsync(notify: false) — used by ReportService.TakeActionAsync so notification can be
    // deferred until after its transaction commits — must persist the hide without notifying.
    [Fact]
    public async Task HideAsync_NotifyFalse_SkipsNotification()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        f.Repo.Setup(r => r.GetByIdAsync(package.Id)).ReturnsAsync(package);

        await f.Service.HideAsync(package.Id, "Allergen mislabeled", notify: false);

        Assert.True(package.IsHidden);
        f.NotificationService.Verify(n => n.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task UnhideAsync_Admin_ClearsHiddenState()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        package.IsHidden = true;
        package.HiddenReason = "Allergen mislabeled";
        f.Repo.Setup(r => r.GetByIdAsync(package.Id)).ReturnsAsync(package);

        await f.Service.UnhideAsync(package.Id);

        Assert.False(package.IsHidden);
        Assert.Null(package.HiddenReason);
    }

    // ---- PackageStockBroadcaster (Phase 12) ---------------------------------

    [Fact]
    public async Task AdjustQuantityManyAsync_BroadcastsOncePerDistinctBusiness()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var businessId = Guid.NewGuid();
        var packages = new List<Package> { TestData.Package(businessId, quantity: 2), TestData.Package(businessId, quantity: 5) };
        f.Repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync(packages);

        var broadcasts = new List<Guid>();
        f.StockBroadcaster.BusinessStockChanged += broadcasts.Add;

        await f.Service.AdjustQuantityManyAsync(packages.Select(p => p.Id).ToList(), 3);

        // Both packages share one business — broadcast once, not twice.
        Assert.Equal([businessId], broadcasts);
    }

    [Fact]
    public async Task AdjustQuantityManyAsync_Unauthorized_DoesNotBroadcast()
    {
        var f = Build(ManagerId, AppRoles.BusinessManager);
        var ownBusinessId = Guid.NewGuid();
        var otherBusinessId = Guid.NewGuid();
        var owned = TestData.Package(ownBusinessId, quantity: 2);
        var notOwned = TestData.Package(otherBusinessId, quantity: 2);
        f.Repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([owned, notOwned]);
        f.BusinessService.Setup(b => b.IsStaffAsync(ownBusinessId, ManagerId)).ReturnsAsync(true);
        f.BusinessService.Setup(b => b.IsStaffAsync(otherBusinessId, ManagerId)).ReturnsAsync(false);

        var broadcasts = new List<Guid>();
        f.StockBroadcaster.BusinessStockChanged += broadcasts.Add;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.AdjustQuantityManyAsync([owned.Id, notOwned.Id], 5));

        Assert.Empty(broadcasts);
    }

    [Fact]
    public async Task DuplicateManyAsync_BroadcastsBusinessChanged()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 4);
        f.Repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([package]);

        var broadcasts = new List<Guid>();
        f.StockBroadcaster.BusinessStockChanged += broadcasts.Add;

        await f.Service.DuplicateManyAsync([package.Id]);

        Assert.Equal([businessId], broadcasts);
    }

    [Fact]
    public async Task ExtendPickupWindowManyAsync_BroadcastsBusinessChanged()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        f.Repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([package]);

        var broadcasts = new List<Guid>();
        f.StockBroadcaster.BusinessStockChanged += broadcasts.Add;

        await f.Service.ExtendPickupWindowManyAsync([package.Id], TimeSpan.FromHours(2));

        Assert.Equal([businessId], broadcasts);
    }

    [Fact]
    public async Task HideAsync_BroadcastsBusinessChanged()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        f.Repo.Setup(r => r.GetByIdAsync(package.Id)).ReturnsAsync(package);

        var broadcasts = new List<Guid>();
        f.StockBroadcaster.BusinessStockChanged += broadcasts.Add;

        await f.Service.HideAsync(package.Id, "reason");

        Assert.Equal([businessId], broadcasts);
    }

    [Fact]
    public async Task UnhideAsync_BroadcastsBusinessChanged()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        package.IsHidden = true;
        f.Repo.Setup(r => r.GetByIdAsync(package.Id)).ReturnsAsync(package);

        var broadcasts = new List<Guid>();
        f.StockBroadcaster.BusinessStockChanged += broadcasts.Add;

        await f.Service.UnhideAsync(package.Id);

        Assert.Equal([businessId], broadcasts);
    }
}
