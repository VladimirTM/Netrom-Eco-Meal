using Moq;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services;
using NetromEcoMeal.Services.Interfaces;
using NetromEcoMeal.Tests.TestSupport;

namespace NetromEcoMeal.Tests.Services;

// Covers KitchenTipService's authorization (customer-only submit, admin-only hide) and the
// trim/clamp behavior on a tip's free-text field.
public class KitchenTipServiceTests
{
    private const string CustomerId = "customer-1";
    private const string AdminId = "admin-1";
    private static readonly Guid BusinessId = Guid.NewGuid();

    private sealed record Fixture(KitchenTipService Service, Mock<IKitchenTipRepository> Repo, Mock<INotificationService> NotificationService, Mock<IAuditLogService> AuditLogService);

    private static Fixture Build(string? userId, params string[] roles)
    {
        var repo = new Mock<IKitchenTipRepository>();
        var notificationService = new Mock<INotificationService>();
        var auditLogService = new Mock<IAuditLogService>();
        var currentUser = new FakeCurrentUser(userId, roles);
        var service = new KitchenTipService(repo.Object, notificationService.Object, auditLogService.Object, currentUser);
        return new Fixture(service, repo, notificationService, auditLogService);
    }

    [Fact]
    public async Task SubmitAsync_NonCustomer_Throws()
    {
        var f = Build(CustomerId, AppRoles.BusinessManager);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SubmitAsync(BusinessId, "Use the side door"));
    }

    [Fact]
    public async Task SubmitAsync_Anonymous_Throws()
    {
        var f = Build(null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SubmitAsync(BusinessId, "Use the side door"));
    }

    [Fact]
    public async Task SubmitAsync_BlankTip_Throws()
    {
        var f = Build(CustomerId, AppRoles.Customer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SubmitAsync(BusinessId, "   "));
    }

    [Fact]
    public async Task SubmitAsync_TooLong_ClampsToMaxLength()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var tooLong = new string('a', KitchenTips.MaxLength + 50);

        var tip = await f.Service.SubmitAsync(BusinessId, tooLong);

        Assert.Equal(KitchenTips.MaxLength, tip.Tip.Length);
    }

    [Fact]
    public async Task SubmitAsync_ValidTip_PersistsTrimmedText()
    {
        var f = Build(CustomerId, AppRoles.Customer);

        var tip = await f.Service.SubmitAsync(BusinessId, "  Use the side door after 8pm  ");

        Assert.Equal("Use the side door after 8pm", tip.Tip);
        Assert.Equal(CustomerId, tip.UserId);
        Assert.Equal(BusinessId, tip.BusinessId);
        f.Repo.Verify(r => r.AddAsync(It.IsAny<KitchenTip>()), Times.Once);
    }

    [Fact]
    public async Task HideAsync_NonAdmin_Throws()
    {
        var f = Build(CustomerId, AppRoles.Customer);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.HideAsync(Guid.NewGuid(), "reason"));
    }

    [Fact]
    public async Task HideAsync_UnknownTip_ReturnsNull()
    {
        var f = Build(AdminId, AppRoles.Admin);
        f.Repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((KitchenTip?)null);

        Assert.Null(await f.Service.HideAsync(Guid.NewGuid(), "reason"));
    }

    [Fact]
    public async Task HideAsync_ExistingTip_HidesAndNotifiesAuthor()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var tip = new KitchenTip { Id = Guid.NewGuid(), BusinessId = BusinessId, UserId = CustomerId, Tip = "t", CreatedAt = DateTime.UtcNow };
        f.Repo.Setup(r => r.GetByIdAsync(tip.Id)).ReturnsAsync(tip);

        var hidden = await f.Service.HideAsync(tip.Id, "Reported as spam");

        Assert.NotNull(hidden);
        Assert.True(hidden!.IsHidden);
        Assert.Equal("Reported as spam", hidden.HiddenReason);
        f.NotificationService.Verify(n => n.CreateAsync(CustomerId, It.IsAny<string>(), null), Times.Once);
    }

    [Fact]
    public async Task HideAsync_NotifyFalse_SkipsNotification()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var tip = new KitchenTip { Id = Guid.NewGuid(), BusinessId = BusinessId, UserId = CustomerId, Tip = "t", CreatedAt = DateTime.UtcNow };
        f.Repo.Setup(r => r.GetByIdAsync(tip.Id)).ReturnsAsync(tip);

        await f.Service.HideAsync(tip.Id, "reason", notify: false);

        f.NotificationService.Verify(n => n.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }
}
