using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class ReferralService(
    IReferralRepository referralRepository,
    IStoreCreditRepository storeCreditRepository,
    IOrderRepository orderRepository,
    UserManager<ApplicationUser> userManager,
    INotificationService notificationService,
    CurrentUserAccessor currentUser) : IReferralService
{
    public async Task<ReferralInfo> GetMyReferralInfoAsync()
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to view your referral info.");

        var user = await userManager.FindByIdAsync(userId)
            ?? throw new UnauthorizedAccessException("You must be signed in to view your referral info.");

        var code = user.ReferralCode ?? await GenerateAndAssignCodeAsync(user);
        var balance = await storeCreditRepository.GetBalanceAsync(userId);
        var referrals = await referralRepository.GetByReferrerUserIdAsync(userId);

        return new ReferralInfo(code, balance,
            referrals.Select(r => new ReferralRow(r.Referred.Name, r.CreatedAt, r.RewardedAt is not null)).ToList());
    }

    public async Task RegisterReferralAsync(string? referralCode, string referredUserId)
    {
        if (string.IsNullOrWhiteSpace(referralCode))
            return;

        var referrer = await userManager.Users.FirstOrDefaultAsync(u => u.ReferralCode == referralCode);
        if (referrer is null || referrer.Id == referredUserId)
            return;

        await referralRepository.AddAsync(new Referral
        {
            Id = Guid.NewGuid(),
            ReferrerUserId = referrer.Id,
            ReferredUserId = referredUserId,
            CreatedAt = DateTime.UtcNow,
        });
        await referralRepository.SaveChangesAsync();
    }

    public async Task TryRewardFirstCompletionAsync(string userId)
    {
        var referral = await referralRepository.GetPendingByReferredUserIdAsync(userId);
        if (referral is null)
            return;

        // Only the referred user's very first Completed order ever unlocks the reward — a repeat
        // customer who happened to sign up with a friend's code doesn't farm it on every order.
        var totalCompleted = await orderRepository.GetTotalCompletedOrderCountAsync(userId);
        if (totalCompleted != 1)
            return;

        var now = DateTime.UtcNow;
        referral.RewardedAt = now;

        await storeCreditRepository.AddAsync(new StoreCreditEntry
        {
            Id = Guid.NewGuid(), UserId = referral.ReferrerUserId, Amount = ReferralCredit.ReferrerAmount,
            Reason = "Referral bonus — your friend completed their first order", CreatedAt = now,
        });
        await storeCreditRepository.AddAsync(new StoreCreditEntry
        {
            Id = Guid.NewGuid(), UserId = referral.ReferredUserId, Amount = ReferralCredit.RefereeAmount,
            Reason = "Welcome bonus — your first completed order", CreatedAt = now,
        });
        await referralRepository.SaveChangesAsync();

        await notificationService.CreateAsync(referral.ReferrerUserId,
            $"Your friend completed their first order — you earned {ReferralCredit.ReferrerAmount:C} in store credit!", "/referrals");
        await notificationService.CreateAsync(referral.ReferredUserId,
            $"Welcome bonus unlocked — you earned {ReferralCredit.RefereeAmount:C} in store credit for your first order!", "/referrals");
    }

    public async Task<decimal> GetAvailableBalanceAsync(string userId)
    {
        return await storeCreditRepository.GetBalanceAsync(userId);
    }

    public async Task DebitAsync(string userId, decimal amount, Guid orderId)
    {
        if (amount <= 0)
            return;

        await storeCreditRepository.AddAsync(new StoreCreditEntry
        {
            Id = Guid.NewGuid(), UserId = userId, Amount = -amount, Reason = "Applied to checkout",
            RelatedOrderId = orderId, CreatedAt = DateTime.UtcNow,
        });
        await storeCreditRepository.SaveChangesAsync();
    }

    private async Task<string> GenerateAndAssignCodeAsync(ApplicationUser user)
    {
        // Collision odds on a 16^8 space are negligible at this app's scale — a handful of
        // retries is only ever a defensive backstop, not something expected to actually trigger.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = RandomNumberGenerator.GetHexString(8).ToUpperInvariant();
            if (await userManager.Users.AnyAsync(u => u.ReferralCode == candidate))
                continue;

            user.ReferralCode = candidate;
            var result = await userManager.UpdateAsync(user);
            if (result.Succeeded)
                return candidate;
        }

        throw new InvalidOperationException("Couldn't generate a referral code — please try again.");
    }
}
