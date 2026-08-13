using Moq;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// Covers the Phase 11 write side of BusinessType — admin-only, and the "in use" delete guard
// that stands in for the cascade-delete landmine noted on IBusinessTypeRepository.IsInUseAsync
// (Business.BusinessTypeId has no explicit OnDelete, so EF Core defaults to Cascade).
public class BusinessTypeServiceTests
{
    private const string AdminId = "admin-1";
    private const string CustomerId = "customer-1";

    private sealed record Fixture(BusinessTypeService Service, Mock<IBusinessTypeRepository> Repo, Mock<IAuditLogService> AuditLog);

    private static Fixture Build(string? userId, params string[] roles)
    {
        var repo = new Mock<IBusinessTypeRepository>();
        var auditLog = new Mock<IAuditLogService>();
        var currentUser = new CurrentUserAccessor(new FakeAuthenticationStateProvider(userId, roles));
        var service = new BusinessTypeService(repo.Object, currentUser, auditLog.Object);
        return new Fixture(service, repo, auditLog);
    }

    [Fact]
    public async Task AddAsync_NonAdmin_Throws()
    {
        var f = Build(CustomerId, AppRoles.Customer);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.AddAsync(new BusinessType { Name = "Food Truck" }));
    }

    [Fact]
    public async Task AddAsync_Admin_TrimsNameAndPersists()
    {
        var f = Build(AdminId, AppRoles.Admin);

        await f.Service.AddAsync(new BusinessType { Name = "  Food Truck  " });

        f.Repo.Verify(r => r.AddAsync(It.Is<BusinessType>(t => t.Name == "Food Truck")), Times.Once);
        f.Repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NonAdmin_Throws()
    {
        var f = Build(CustomerId, AppRoles.Customer);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_StillInUse_ThrowsInvalidOperationAndDoesNotDelete()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var type = new BusinessType { Id = Guid.NewGuid(), Name = "Bakery" };
        f.Repo.Setup(r => r.GetByIdAsync(type.Id)).ReturnsAsync(type);
        f.Repo.Setup(r => r.IsInUseAsync(type.Id)).ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.DeleteAsync(type.Id));

        f.Repo.Verify(r => r.DeleteAsync(It.IsAny<BusinessType>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_NotInUse_Deletes()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var type = new BusinessType { Id = Guid.NewGuid(), Name = "Bakery" };
        f.Repo.Setup(r => r.GetByIdAsync(type.Id)).ReturnsAsync(type);
        f.Repo.Setup(r => r.IsInUseAsync(type.Id)).ReturnsAsync(false);

        await f.Service.DeleteAsync(type.Id);

        f.Repo.Verify(r => r.DeleteAsync(type), Times.Once);
        f.Repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_IsNoOp()
    {
        var f = Build(AdminId, AppRoles.Admin);
        f.Repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((BusinessType?)null);

        await f.Service.DeleteAsync(Guid.NewGuid());

        f.Repo.Verify(r => r.DeleteAsync(It.IsAny<BusinessType>()), Times.Never);
    }
}
