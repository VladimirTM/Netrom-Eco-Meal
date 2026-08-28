using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Email;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

// AI-scored trigger instead of a hand-coded rule: rather than every favorite getting every alert
// (see PackageService.NotifyFavoritingCustomersAsync), this only nudges a business's favoriters
// and past customers when one of its packages is actually at risk — closing soon and still
// unclaimed — and personalizes the copy when the customer's completed-order history at that
// business shares a dietary tag with the package.
public class NearExpiryNudgeService(
    IPackageRepository packageRepository,
    IFavoriteRepository favoriteRepository,
    IOrderRepository orderRepository,
    INotificationService notificationService,
    IAppEmailSender emailSender,
    INearExpiryNudgeComposer composer,
    IConfiguration configuration) : INearExpiryNudgeService
{
    // Same fallback/config-key convention as OrderService.BaseUrl/PackageService.BaseUrl.
    private string BaseUrl => (configuration["App:BaseUrl"] ?? "http://localhost:8080").TrimEnd('/');

    public async Task<int> SweepAsync()
    {
        var now = DateTime.UtcNow;
        var candidates = await packageRepository.GetNearExpiryUnclaimedAsync(now, now + PackageNudgeSettings.ClosingSoonWindow);
        if (candidates.Count == 0)
            return 0;

        var sentCount = 0;
        foreach (var package in candidates)
        {
            var favoriters = await favoriteRepository.GetFavoritingUsersAsync(package.BusinessId);
            var pastCustomers = await orderRepository.GetPastCustomersAsync(package.BusinessId);
            var audience = favoriters.UnionBy(pastCustomers, u => u.Id).ToList();

            if (audience.Count > 0)
                sentCount += await NotifyAudienceAsync(package, audience, now);

            // Marked whether or not there was anyone to notify — an empty audience this sweep
            // doesn't mean try again next tick, the package just has no interested customers.
            package.NearExpiryNudgeSentAt = now;
        }

        await packageRepository.SaveChangesAsync();
        return sentCount;
    }

    // Groups the audience by whichever of the package's dietary tags (if any) shows up in that
    // customer's own completed-order history at this business, so the AI is only asked to draft
    // one message per distinct match instead of once per customer.
    private async Task<int> NotifyAudienceAsync(Package package, List<ApplicationUser> audience, DateTime now)
    {
        // "" (not null) stands for "no match" — Dictionary<TKey> requires a non-null key.
        var groups = new Dictionary<string, List<ApplicationUser>>();
        foreach (var user in audience)
        {
            var pastPackages = await orderRepository.GetCompletedPackagesAsync(user.Id, package.BusinessId);
            var matchedTag = package.DietaryTags.FirstOrDefault(tag =>
                pastPackages.Any(p => p.DietaryTags.Contains(tag, StringComparer.OrdinalIgnoreCase))) ?? "";

            if (!groups.TryGetValue(matchedTag, out var group))
                groups[matchedTag] = group = [];
            group.Add(user);
        }

        var url = $"/businesses/{package.BusinessId}";
        var sent = 0;
        foreach (var (matchedTag, users) in groups)
        {
            var minutesLeft = Math.Max(1, (int)Math.Round((package.PickupEnd - now).TotalMinutes));
            var message = await composer.ComposeAsync(package.Name, package.Business.Name, package.Quantity, package.PickupEnd - now, matchedTag.Length == 0 ? null : matchedTag);

            foreach (var user in users)
            {
                await notificationService.CreateAsync(user.Id, message, url);

                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    var html = EmailTemplateBuilder.Build(
                        message,
                        [$"\"{package.Name}\" at {package.Business.Name} closes in {minutesLeft} minutes with stock still unclaimed."],
                        eyebrow: package.Business.Name,
                        ctaLabel: "View this kitchen",
                        ctaUrl: $"{BaseUrl}{url}");
                    await emailSender.SendEmailAsync(user.Email, "Eco Meal — Closing soon", html);
                }
            }

            sent += users.Count;
        }

        return sent;
    }
}
