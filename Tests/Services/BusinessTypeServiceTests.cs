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
        // AddAsync/UpdateAsync now check for a name collision via GetAllAsync — an unconfigured
        // Moq setup for a Task<List<T>> return defaults to a null result, not an empty list, the
        // way a real repository against an empty/no-collision table actually behaves.
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
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
    public async Task AddAsync_Admin_DuplicateNameCaseInsensitive_ThrowsAndDoesNotPersist()
    {
        var f = Build(AdminId, AppRoles.Admin);
        f.Repo.Setup(r => r.GetAllAsync()).ReturnsAsync([new BusinessType { Id = Guid.NewGuid(), Name = "Bakery" }]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.AddAsync(new BusinessType { Name = "  bakery  " }));

        f.Repo.Verify(r => r.AddAsync(It.IsAny<BusinessType>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_Admin_DuplicateNameOfAnotherType_ThrowsAndDoesNotRename()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var editing = new BusinessType { Id = Guid.NewGuid(), Name = "Food Truck" };
        var other = new BusinessType { Id = Guid.NewGuid(), Name = "Bakery" };
        f.Repo.Setup(r => r.GetByIdAsync(editing.Id)).ReturnsAsync(editing);
        f.Repo.Setup(r => r.GetAllAsync()).ReturnsAsync([editing, other]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.UpdateAsync(new BusinessType { Id = editing.Id, Name = "Bakery" }));

        Assert.Equal("Food Truck", editing.Name);
        f.Repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_Admin_SameNameAsItself_Persists()
    {
        // Renaming a type back to its own current name (or just editing casing/whitespace) must
        // not be blocked by the duplicate guard matching the type against itself.
        var f = Build(AdminId, AppRoles.Admin);
        var editing = new BusinessType { Id = Guid.NewGuid(), Name = "Food Truck" };
        f.Repo.Setup(r => r.GetByIdAsync(editing.Id)).ReturnsAsync(editing);
        f.Repo.Setup(r => r.GetAllAsync()).ReturnsAsync([editing]);

        await f.Service.UpdateAsync(new BusinessType { Id = editing.Id, Name = "Food Truck" });

        Assert.Equal("Food Truck", editing.Name);
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
