using Microsoft.Extensions.Configuration;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services.Email;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Services;

public class StandingOrderService(
    IStandingOrderRepository standingOrderRepository,
    IOrderRepository orderRepository,
    IBusinessService businessService,
    INotificationService notificationService,
    IAppEmailSender emailSender,
    ICurrentUser currentUser,
    IConfiguration configuration) : IStandingOrderService
{
    private string BaseUrl => (configuration["App:BaseUrl"] ?? "http://localhost:8080").TrimEnd('/');

    public async Task<List<StandingOrder>> GetMyStandingOrdersAsync()
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            return [];

        return await standingOrderRepository.GetByUserIdAsync(userId);
    }

    public async Task<StandingOrder> CreateAsync(Guid businessId, Guid? packageTypeId, string? dietaryTag, decimal maxWeeklySpend)
    {
        if (!await currentUser.IsInRoleAsync(AppRoles.Customer))
            throw new UnauthorizedAccessException("Only customers can set up a standing order.");

        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to set up a standing order.");

        var business = await businessService.GetByIdAsync(businessId);
        if (business is not { Status: BusinessStatuses.Approved, IsHidden: false })
            throw new InvalidOperationException("This kitchen isn't available right now.");

        if (dietaryTag is not null && !DietaryTags.All.Contains(dietaryTag, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("That dietary tag isn't recognized.");

        var standingOrder = new StandingOrder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BusinessId = businessId,
            PackageTypeId = packageTypeId,
            DietaryTag = dietaryTag,
            MaxWeeklySpend = Math.Clamp(maxWeeklySpend, StandingOrders.MinWeeklySpend, StandingOrders.MaxWeeklySpend),
            CreatedAt = DateTime.UtcNow,
        };

        await standingOrderRepository.AddAsync(standingOrder);
        await standingOrderRepository.SaveChangesAsync();

        return standingOrder;
    }

    public async Task UpdateAsync(Guid id, decimal maxWeeklySpend, bool isActive)
    {
        var standingOrder = await standingOrderRepository.GetByIdAsync(id);
        if (standingOrder is null)
            return;

        await EnsureOwnerAsync(standingOrder);

        standingOrder.MaxWeeklySpend = Math.Clamp(maxWeeklySpend, StandingOrders.MinWeeklySpend, StandingOrders.MaxWeeklySpend);
        standingOrder.IsActive = isActive;
        await standingOrderRepository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var standingOrder = await standingOrderRepository.GetByIdAsync(id);
        if (standingOrder is null)
            return;

        await EnsureOwnerAsync(standingOrder);

        await standingOrderRepository.DeleteAsync(id);
        await standingOrderRepository.SaveChangesAsync();
    }

    public async Task<int> MatchNewPackagesAsync(IEnumerable<Package> generatedPackages)
    {
        var matched = 0;
        var candidatesByBusiness = new Dictionary<Guid, List<StandingOrder>>();
        // A freshly generated package's own Business nav isn't populated (GetActiveAsync loads
        // templates with no includes) — resolved and cached here instead of requerying per match.
        var businessNames = new Dictionary<Guid, string>();

        foreach (var package in generatedPackages)
        {
            if (!candidatesByBusiness.TryGetValue(package.BusinessId, out var candidates))
                candidatesByBusiness[package.BusinessId] = candidates = await standingOrderRepository.GetActiveByBusinessIdAsync(package.BusinessId);

            if (candidates.Count == 0)
                continue;

            if (!businessNames.TryGetValue(package.BusinessId, out var businessName))
                businessNames[package.BusinessId] = businessName = (await businessService.GetByIdAsync(package.BusinessId))?.Name ?? "this kitchen";

            foreach (var standingOrder in candidates)
            {
                if (standingOrder.PackageTypeId is { } typeId && typeId != package.PackageTypeId)
                    continue;

                if (standingOrder.DietaryTag is { } tag && !package.DietaryTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                    continue;

                var windowStart = DateTime.UtcNow - StandingOrders.SpendWindow;
                var spentThisWeek = await orderRepository.GetSpendInRangeAsync(
                    standingOrder.UserId, standingOrder.BusinessId, standingOrder.PackageTypeId, standingOrder.DietaryTag, windowStart, DateTime.UtcNow);

                if (spentThisWeek + package.Price > standingOrder.MaxWeeklySpend)
                    continue;

                await NotifyMatchAsync(standingOrder, package, businessName);
                matched++;
            }
        }

        return matched;
    }

    private async Task NotifyMatchAsync(StandingOrder standingOrder, Package package, string businessName)
    {
        var url = $"/businesses/{package.BusinessId}?reserve={package.Id}";
        var message = $"Your usual just went live at {businessName} — \"{package.Name}\" for {package.Price:C}. We've added it to your basket to confirm.";
        await notificationService.CreateAsync(standingOrder.UserId, message, url);

        var user = standingOrder.User;
        if (user is not null && !string.IsNullOrWhiteSpace(user.Email))
        {
            var html = EmailTemplateBuilder.Build(
                message,
                [$"\"{package.Name}\" ({package.Price:C}) matches a standing order you set up at {businessName}."],
                eyebrow: businessName,
                ctaLabel: "Review your basket",
                ctaUrl: $"{BaseUrl}{url}");
            await emailSender.SendEmailAsync(user.Email, "Eco Meal — Your usual is live", html);
        }
    }

    private async Task EnsureOwnerAsync(StandingOrder standingOrder)
    {
        var (isAdmin, userId) = await currentUser.GetCurrentUserAsync();
        if (isAdmin)
            return;

        if (userId is null || standingOrder.UserId != userId)
            throw new UnauthorizedAccessException("You can only manage your own standing orders.");
    }
}
