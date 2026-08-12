using Moq;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// Covers ReportService's authorization (submit is open to any signed-in user; dismiss/take-action
// are admin-only) and the take-action -> hide-the-target wiring. IReportRepository and the
// Business/Package services it delegates to are mocked. EcoMealDbContext is a real
// InMemory-backed instance since TakeActionAsync wraps its writes in a DB transaction.
public class ReportServiceTests
{
    private const string AdminId = "admin-1";
    private const string CustomerId = "customer-1";

    private sealed record Fixture(ReportService Service, Mock<IReportRepository> Repo, Mock<IBusinessService> BusinessService, Mock<IPackageService> PackageService, EcoMealDbContext Db);

    private static Fixture Build(string? userId, params string[] roles)
    {
        var db = InMemoryDb.Create();
        var repo = new Mock<IReportRepository>();
        var businessService = new Mock<IBusinessService>();
        var packageService = new Mock<IPackageService>();
        var auditLog = new Mock<IAuditLogService>();
        var currentUser = new CurrentUserAccessor(new FakeAuthenticationStateProvider(userId, roles));
        var service = new ReportService(repo.Object, businessService.Object, packageService.Object, auditLog.Object, db, currentUser);
        return new Fixture(service, repo, businessService, packageService, db);
    }

    [Fact]
    public async Task SubmitAsync_Anonymous_Throws()
    {
        var f = Build(null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.SubmitAsync(AuditTargetTypes.Business, Guid.NewGuid(), "reason"));
    }

    [Fact]
    public async Task SubmitAsync_SignedInCustomer_PersistsOpenReport()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var targetId = Guid.NewGuid();

        await f.Service.SubmitAsync(AuditTargetTypes.Business, targetId, "Looks off");

        f.Repo.Verify(r => r.AddAsync(It.Is<Report>(report =>
            report.ReporterUserId == CustomerId &&
            report.TargetId == targetId &&
            report.Status == ReportStatuses.Open)), Times.Once);
    }

    [Fact]
    public async Task GetOpenAsync_NonAdmin_Throws()
    {
        var f = Build(CustomerId, AppRoles.Customer);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetOpenAsync());
    }

    [Fact]
    public async Task DismissAsync_NonAdmin_Throws()
    {
        var f = Build(CustomerId, AppRoles.Customer);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.DismissAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DismissAsync_OpenReport_MarksDismissed()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var report = new Report { Id = Guid.NewGuid(), ReporterUserId = CustomerId, Reporter = TestData.User(CustomerId), TargetType = AuditTargetTypes.Business, TargetId = Guid.NewGuid(), Reason = "x", Status = ReportStatuses.Open, CreatedAt = DateTime.UtcNow };
        f.Repo.Setup(r => r.GetByIdAsync(report.Id)).ReturnsAsync(report);
        f.BusinessService.Setup(b => b.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [report.TargetId] = "Test Business" });

        await f.Service.DismissAsync(report.Id);

        Assert.Equal(ReportStatuses.Dismissed, report.Status);
        Assert.Equal(AdminId, report.ResolvedByUserId);
        f.Repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task TakeActionAsync_BusinessTarget_HidesBusinessAndMarksActionTaken()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var report = new Report { Id = Guid.NewGuid(), ReporterUserId = CustomerId, Reporter = TestData.User(CustomerId), TargetType = AuditTargetTypes.Business, TargetId = Guid.NewGuid(), Reason = "Bad photos", Status = ReportStatuses.Open, CreatedAt = DateTime.UtcNow };
        f.Repo.Setup(r => r.GetByIdAsync(report.Id)).ReturnsAsync(report);
        f.BusinessService.Setup(b => b.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [report.TargetId] = "Test Business" });

        await f.Service.TakeActionAsync(report.Id, "Bad photos");

        // notify: false — TakeActionAsync defers the notification fan-out until after its
        // transaction commits (see BusinessService.NotifyHiddenAsync).
        f.BusinessService.Verify(b => b.HideAsync(report.TargetId, "Bad photos", false), Times.Once);
        Assert.Equal(ReportStatuses.ActionTaken, report.Status);
    }

    [Fact]
    public async Task TakeActionAsync_PackageTarget_HidesPackage()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var report = new Report { Id = Guid.NewGuid(), ReporterUserId = CustomerId, Reporter = TestData.User(CustomerId), TargetType = AuditTargetTypes.Package, TargetId = Guid.NewGuid(), Reason = "Mislabeled", Status = ReportStatuses.Open, CreatedAt = DateTime.UtcNow };
        f.Repo.Setup(r => r.GetByIdAsync(report.Id)).ReturnsAsync(report);
        f.PackageService.Setup(p => p.GetNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [report.TargetId] = "Test Package" });

        await f.Service.TakeActionAsync(report.Id, "Mislabeled");

        f.PackageService.Verify(p => p.HideAsync(report.TargetId, "Mislabeled", false), Times.Once);
        Assert.Equal(ReportStatuses.ActionTaken, report.Status);
    }
}
