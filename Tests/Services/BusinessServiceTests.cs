using Moq;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// Covers BusinessService's staff-assignment authorization (admin-only, per the many-to-many
// BusinessStaff join table that replaced the single Business.ManagerId) and pass-through
// delegation to IBusinessRepository. IBusinessRepository is mocked; the repository's own
// CRUD/uniqueness behavior is covered separately in BusinessRepositoryTests against a real
// InMemory-backed EcoMealDbContext.
public class BusinessServiceTests
{
    private const string AdminId = "admin-1";
    private const string ManagerId = "manager-1";

    private sealed record Fixture(BusinessService Service, Mock<IBusinessRepository> Repo);

    private static Fixture Build(string? userId, params string[] roles)
    {
        var repo = new Mock<IBusinessRepository>();
        var currentUser = new CurrentUserAccessor(new FakeAuthenticationStateProvider(userId, roles));
        var service = new BusinessService(repo.Object, currentUser);
        return new Fixture(service, repo);
    }

    [Fact]
    public async Task AddStaffAsync_NonAdmin_Throws()
    {
        var f = Build(ManagerId, Constants.AppRoles.BusinessManager);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.AddStaffAsync(Guid.NewGuid(), ManagerId));
    }

    [Fact]
    public async Task AddStaffAsync_Admin_DelegatesToRepository()
    {
        var f = Build(AdminId, Constants.AppRoles.Admin);
        var businessId = Guid.NewGuid();
        f.Repo.Setup(r => r.AddStaffAsync(businessId, ManagerId)).ReturnsAsync(true);

        var result = await f.Service.AddStaffAsync(businessId, ManagerId);

        Assert.True(result);
        f.Repo.Verify(r => r.AddStaffAsync(businessId, ManagerId), Times.Once);
    }

    [Fact]
    public async Task RemoveStaffAsync_NonAdmin_Throws()
    {
        var f = Build(ManagerId, Constants.AppRoles.BusinessManager);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.RemoveStaffAsync(Guid.NewGuid(), ManagerId));
    }

    [Fact]
    public async Task RemoveStaffAsync_Admin_DelegatesToRepository()
    {
        var f = Build(AdminId, Constants.AppRoles.Admin);
        var businessId = Guid.NewGuid();
        f.Repo.Setup(r => r.RemoveStaffAsync(businessId, ManagerId)).ReturnsAsync(true);

        var result = await f.Service.RemoveStaffAsync(businessId, ManagerId);

        Assert.True(result);
        f.Repo.Verify(r => r.RemoveStaffAsync(businessId, ManagerId), Times.Once);
    }

    [Fact]
    public async Task IsStaffAsync_DelegatesToRepository()
    {
        var f = Build(AdminId, Constants.AppRoles.Admin);
        var businessId = Guid.NewGuid();
        f.Repo.Setup(r => r.IsStaffAsync(businessId, ManagerId)).ReturnsAsync(true);

        Assert.True(await f.Service.IsStaffAsync(businessId, ManagerId));
    }

    [Fact]
    public async Task GetByStaffUserIdAsync_OneUserStaffingTwoBusinesses_ReturnsBoth()
    {
        var f = Build(AdminId, Constants.AppRoles.Admin);
        var businesses = new List<Business> { TestData.Business(), TestData.Business() };
        f.Repo.Setup(r => r.GetByStaffUserIdAsync(ManagerId)).ReturnsAsync(businesses);

        var result = await f.Service.GetByStaffUserIdAsync(ManagerId);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task UpdateAsync_StaffOfBusiness_Succeeds()
    {
        var f = Build(ManagerId, Constants.AppRoles.BusinessManager);
        var business = TestData.Business();
        f.Repo.Setup(r => r.GetByIdAsync(business.Id)).ReturnsAsync(business);
        f.Repo.Setup(r => r.IsStaffAsync(business.Id, ManagerId)).ReturnsAsync(true);

        business.Name = "Updated Name";
        await f.Service.UpdateAsync(business);

        f.Repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NotStaffOfBusiness_Throws()
    {
        var f = Build(ManagerId, Constants.AppRoles.BusinessManager);
        var business = TestData.Business();
        f.Repo.Setup(r => r.GetByIdAsync(business.Id)).ReturnsAsync(business);
        f.Repo.Setup(r => r.IsStaffAsync(business.Id, ManagerId)).ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.UpdateAsync(business));
    }
}
