using Moq;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// Same shape as BusinessTypeServiceTests — PackageType's write side mirrors BusinessType's
// exactly (admin-only, blocked while still referenced by a Package).
public class PackageTypeServiceTests
{
    private const string AdminId = "admin-1";
    private const string CustomerId = "customer-1";

    private sealed record Fixture(PackageTypeService Service, Mock<IPackageTypeRepository> Repo, Mock<IAuditLogService> AuditLog);

    private static Fixture Build(string? userId, params string[] roles)
    {
        var repo = new Mock<IPackageTypeRepository>();
        var auditLog = new Mock<IAuditLogService>();
        var currentUser = new CurrentUserAccessor(new FakeAuthenticationStateProvider(userId, roles));
        var service = new PackageTypeService(repo.Object, currentUser, auditLog.Object);
        return new Fixture(service, repo, auditLog);
    }

    [Fact]
    public async Task AddAsync_NonAdmin_Throws()
    {
        var f = Build(CustomerId, AppRoles.Customer);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.AddAsync(new PackageType { Name = "Salad Box" }));
    }

    [Fact]
    public async Task DeleteAsync_StillInUse_ThrowsInvalidOperationAndDoesNotDelete()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var type = new PackageType { Id = Guid.NewGuid(), Name = "Meal Box" };
        f.Repo.Setup(r => r.GetByIdAsync(type.Id)).ReturnsAsync(type);
        f.Repo.Setup(r => r.IsInUseAsync(type.Id)).ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.DeleteAsync(type.Id));

        f.Repo.Verify(r => r.DeleteAsync(It.IsAny<PackageType>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_NotInUse_Deletes()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var type = new PackageType { Id = Guid.NewGuid(), Name = "Meal Box" };
        f.Repo.Setup(r => r.GetByIdAsync(type.Id)).ReturnsAsync(type);
        f.Repo.Setup(r => r.IsInUseAsync(type.Id)).ReturnsAsync(false);

        await f.Service.DeleteAsync(type.Id);

        f.Repo.Verify(r => r.DeleteAsync(type), Times.Once);
        f.Repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
