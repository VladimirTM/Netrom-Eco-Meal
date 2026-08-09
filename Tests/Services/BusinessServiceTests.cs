using Moq;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;
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

    private sealed record Fixture(BusinessService Service, Mock<IBusinessRepository> Repo, Mock<IAuditLogService> AuditLog, Mock<INotificationService> Notification);

    private static Fixture Build(string? userId, params string[] roles)
    {
        var repo = new Mock<IBusinessRepository>();
        var auditLog = new Mock<IAuditLogService>();
        var notification = new Mock<INotificationService>();
        var currentUser = new CurrentUserAccessor(new FakeAuthenticationStateProvider(userId, roles));
        var service = new BusinessService(repo.Object, currentUser, auditLog.Object, notification.Object);
        return new Fixture(service, repo, auditLog, notification);
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

    // ---- ApplyAsync (self-service signup) --------------------------------

    [Fact]
    public async Task ApplyAsync_Anonymous_Throws()
    {
        var f = Build(null);
        var business = TestData.Business();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.ApplyAsync(business));
    }

    [Fact]
    public async Task ApplyAsync_SignedInCustomer_SetsPendingApprovalAndSubmitter()
    {
        var f = Build(ManagerId, Constants.AppRoles.Customer);
        var business = TestData.Business();

        var result = await f.Service.ApplyAsync(business);

        Assert.Equal(Constants.BusinessStatuses.PendingApproval, result.Status);
        Assert.Equal(ManagerId, result.SubmittedByUserId);
        f.Repo.Verify(r => r.AddAsync(business), Times.Once);
        f.Repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ---- ApproveAsync / RejectAsync ---------------------------------------

    [Fact]
    public async Task ApproveAsync_NonAdmin_Throws()
    {
        var f = Build(ManagerId, Constants.AppRoles.BusinessManager);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.ApproveAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ApproveAsync_PendingBusiness_SetsApprovedAndNotifiesSubmitter()
    {
        var f = Build(AdminId, Constants.AppRoles.Admin);
        var business = TestData.Business();
        business.Status = Constants.BusinessStatuses.PendingApproval;
        business.SubmittedByUserId = ManagerId;
        f.Repo.Setup(r => r.GetByIdAsync(business.Id)).ReturnsAsync(business);

        await f.Service.ApproveAsync(business.Id);

        Assert.Equal(Constants.BusinessStatuses.Approved, business.Status);
        f.Repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        f.Notification.Verify(n => n.CreateAsync(ManagerId, It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_RejectedBusiness_CanBeReconsideredToApproved()
    {
        var f = Build(AdminId, Constants.AppRoles.Admin);
        var business = TestData.Business();
        business.Status = Constants.BusinessStatuses.Rejected;
        business.RejectionReason = "Bad address";
        f.Repo.Setup(r => r.GetByIdAsync(business.Id)).ReturnsAsync(business);

        await f.Service.ApproveAsync(business.Id);

        Assert.Equal(Constants.BusinessStatuses.Approved, business.Status);
        Assert.Null(business.RejectionReason);
    }

    [Fact]
    public async Task RejectAsync_NonAdmin_Throws()
    {
        var f = Build(ManagerId, Constants.AppRoles.BusinessManager);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.RejectAsync(Guid.NewGuid(), "reason"));
    }

    [Fact]
    public async Task RejectAsync_PendingBusiness_SetsRejectedWithReason()
    {
        var f = Build(AdminId, Constants.AppRoles.Admin);
        var business = TestData.Business();
        business.Status = Constants.BusinessStatuses.PendingApproval;
        f.Repo.Setup(r => r.GetByIdAsync(business.Id)).ReturnsAsync(business);

        await f.Service.RejectAsync(business.Id, "Bad address");

        Assert.Equal(Constants.BusinessStatuses.Rejected, business.Status);
        Assert.Equal("Bad address", business.RejectionReason);
    }

    // ---- HideAsync / UnhideAsync -------------------------------------------

    [Fact]
    public async Task HideAsync_NonAdmin_Throws()
    {
        var f = Build(ManagerId, Constants.AppRoles.BusinessManager);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.HideAsync(Guid.NewGuid(), "reason"));
    }

    [Fact]
    public async Task HideAsync_Admin_SetsHiddenWithReasonAndNotifiesStaff()
    {
        var f = Build(AdminId, Constants.AppRoles.Admin);
        var business = TestData.Business();
        business.Staff.Add(TestData.BusinessStaff(business.Id, ManagerId));
        f.Repo.Setup(r => r.GetByIdAsync(business.Id)).ReturnsAsync(business);

        await f.Service.HideAsync(business.Id, "Food safety concern");

        Assert.True(business.IsHidden);
        Assert.Equal("Food safety concern", business.HiddenReason);
        f.Notification.Verify(n => n.CreateAsync(ManagerId, It.IsAny<string>(), null), Times.Once);
    }

    [Fact]
    public async Task UnhideAsync_Admin_ClearsHiddenState()
    {
        var f = Build(AdminId, Constants.AppRoles.Admin);
        var business = TestData.Business();
        business.IsHidden = true;
        business.HiddenReason = "Food safety concern";
        f.Repo.Setup(r => r.GetByIdAsync(business.Id)).ReturnsAsync(business);

        await f.Service.UnhideAsync(business.Id);

        Assert.False(business.IsHidden);
        Assert.Null(business.HiddenReason);
    }
}
