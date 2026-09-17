using Microsoft.AspNetCore.Identity;
using Moq;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// GetMyReferralInfoAsync/RegisterReferralAsync query userManager.Users directly, which needs a
// real async LINQ provider a mock can't give — so this only covers what's left:
// TryRewardFirstCompletionAsync's reward gate and the plain GetAvailableBalanceAsync/DebitAsync ledger ops.
public class ReferralServiceTests
{
    private const string ReferrerId = "referrer-1";
    private const string ReferredId = "referred-1";

    private static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private sealed record Fixture(ReferralService Service, Mock<IReferralRepository> ReferralRepo, Mock<IStoreCreditRepository> StoreCreditRepo, Mock<IOrderRepository> OrderRepo, Mock<INotificationService> NotificationService);

    private static Fixture Build()
    {
        var referralRepo = new Mock<IReferralRepository>();
        var storeCreditRepo = new Mock<IStoreCreditRepository>();
        var orderRepo = new Mock<IOrderRepository>();
        var notificationService = new Mock<INotificationService>();
        var currentUser = new CurrentUserAccessor(new FakeAuthenticationStateProvider(null));
        var service = new ReferralService(referralRepo.Object, storeCreditRepo.Object, orderRepo.Object, MockUserManager().Object, notificationService.Object, currentUser);
        return new Fixture(service, referralRepo, storeCreditRepo, orderRepo, notificationService);
    }

    private static Referral PendingReferral() => new()
    {
        Id = Guid.NewGuid(), ReferrerUserId = ReferrerId, ReferredUserId = ReferredId, CreatedAt = DateTime.UtcNow.AddDays(-3),
    };

    // ---- TryRewardFirstCompletionAsync ---------------------------------------

    [Fact]
    public async Task TryRewardFirstCompletionAsync_NoPendingReferral_DoesNothing()
    {
        var f = Build();
        f.ReferralRepo.Setup(r => r.GetPendingByReferredUserIdAsync(ReferredId)).ReturnsAsync((Referral?)null);

        await f.Service.TryRewardFirstCompletionAsync(ReferredId);

        f.StoreCreditRepo.Verify(r => r.AddAsync(It.IsAny<StoreCreditEntry>()), Times.Never);
    }

    [Fact]
    public async Task TryRewardFirstCompletionAsync_NotGenuinelyFirstCompletedOrder_DoesNothingAndLeavesReferralPending()
    {
        var f = Build();
        var referral = PendingReferral();
        f.ReferralRepo.Setup(r => r.GetPendingByReferredUserIdAsync(ReferredId)).ReturnsAsync(referral);
        // A repeat customer who happens to have an old pending referral row shouldn't farm the
        // reward on every subsequent order.
        f.OrderRepo.Setup(r => r.GetTotalCompletedOrderCountAsync(ReferredId)).ReturnsAsync(2);

        await f.Service.TryRewardFirstCompletionAsync(ReferredId);

        f.StoreCreditRepo.Verify(r => r.AddAsync(It.IsAny<StoreCreditEntry>()), Times.Never);
        Assert.Null(referral.RewardedAt);
    }

    [Fact]
    public async Task TryRewardFirstCompletionAsync_GenuinelyFirstCompletedOrder_CreditsBothPartiesAndMarksRewarded()
    {
        var f = Build();
        var referral = PendingReferral();
        f.ReferralRepo.Setup(r => r.GetPendingByReferredUserIdAsync(ReferredId)).ReturnsAsync(referral);
        f.OrderRepo.Setup(r => r.GetTotalCompletedOrderCountAsync(ReferredId)).ReturnsAsync(1);

        await f.Service.TryRewardFirstCompletionAsync(ReferredId);

        Assert.NotNull(referral.RewardedAt);
        f.StoreCreditRepo.Verify(r => r.AddAsync(It.Is<StoreCreditEntry>(e => e.UserId == ReferrerId && e.Amount == ReferralCredit.ReferrerAmount)), Times.Once);
        f.StoreCreditRepo.Verify(r => r.AddAsync(It.Is<StoreCreditEntry>(e => e.UserId == ReferredId && e.Amount == ReferralCredit.RefereeAmount)), Times.Once);
        f.NotificationService.Verify(n => n.CreateAsync(ReferrerId, It.IsAny<string>(), "/referrals"), Times.Once);
        f.NotificationService.Verify(n => n.CreateAsync(ReferredId, It.IsAny<string>(), "/referrals"), Times.Once);
    }

    // ---- GetAvailableBalanceAsync / DebitAsync -------------------------------

    [Fact]
    public async Task GetAvailableBalanceAsync_DelegatesToRepository()
    {
        var f = Build();
        f.StoreCreditRepo.Setup(r => r.GetBalanceAsync(ReferrerId)).ReturnsAsync(25m);

        Assert.Equal(25m, await f.Service.GetAvailableBalanceAsync(ReferrerId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task DebitAsync_ZeroOrNegativeAmount_DoesNothing(decimal amount)
    {
        var f = Build();

        await f.Service.DebitAsync(ReferrerId, amount, Guid.NewGuid());

        f.StoreCreditRepo.Verify(r => r.AddAsync(It.IsAny<StoreCreditEntry>()), Times.Never);
    }

    [Fact]
    public async Task DebitAsync_PositiveAmount_AddsNegativeLedgerEntry()
    {
        var f = Build();
        var orderId = Guid.NewGuid();

        await f.Service.DebitAsync(ReferrerId, 7.5m, orderId);

        f.StoreCreditRepo.Verify(r => r.AddAsync(It.Is<StoreCreditEntry>(e => e.UserId == ReferrerId && e.Amount == -7.5m && e.RelatedOrderId == orderId)), Times.Once);
    }
}
