using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services.Payments;

public class CheckoutService(
    IStripeGateway stripeGateway,
    IOrderService orderService,
    IBusinessService businessService,
    IPackageRepository packageRepository,
    EcoMealDbContext dbContext,
    CurrentUserAccessor currentUser,
    IConfiguration configuration) : ICheckoutService
{
    public async Task<string> StartCheckoutAsync(Guid businessId, List<OrderLineRequest> lines)
    {
        if (!await currentUser.IsInRoleAsync(AppRoles.Customer))
            throw new UnauthorizedAccessException("Only customers can check out.");

        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to check out.");

        if (lines.Count == 0)
            throw new InvalidOperationException("Your cart is empty.");

        var business = await businessService.GetByIdAsync(businessId)
            ?? throw new InvalidOperationException("This business is no longer available.");

        var packageIds = lines.Select(l => l.PackageId).Distinct().ToList();
        var packages = (await packageRepository.GetByIdsAsync(packageIds)).ToDictionary(p => p.Id);

        // Pre-checkout availability check — a courtesy so nobody pays for stock that's already
        // gone. PlaceOrderAsync re-checks this for real once payment actually succeeds.
        var checkoutLines = new List<CheckoutLineItem>();
        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                throw new InvalidOperationException("Quantity must be greater than zero.");

            if (!packages.TryGetValue(line.PackageId, out var package) || package.BusinessId != businessId)
                throw new InvalidOperationException("One of the packages in your cart is no longer available.");

            var pendingElsewhere = await dbContext.OrderPackages
                .Where(op => op.PackageId == package.Id && op.Order.Status.Name == OrderStatuses.Pending)
                .SumAsync(op => (int?)op.Quantity) ?? 0;
            var available = package.Quantity - pendingElsewhere;

            if (line.Quantity > available)
                throw new InvalidOperationException($"Only {Math.Max(available, 0)} left of \"{package.Name}\" — please adjust your cart.");

            checkoutLines.Add(new CheckoutLineItem(package.Name, package.Price, line.Quantity));
        }

        var pendingCheckout = new PendingCheckout
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BusinessId = businessId,
            LinesJson = JsonSerializer.Serialize(lines),
            CreatedAt = DateTime.UtcNow,
        };
        dbContext.PendingCheckouts.Add(pendingCheckout);
        await dbContext.SaveChangesAsync();

        var baseUrl = (configuration["App:BaseUrl"] ?? "http://localhost:8080").TrimEnd('/');
        // Stripe replaces the literal "{CHECKOUT_SESSION_ID}" token itself once the session exists.
        var successUrl = $"{baseUrl}/checkout/return?pc={pendingCheckout.Id}&session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{baseUrl}/checkout/cancel?pc={pendingCheckout.Id}";

        var session = await stripeGateway.CreateCheckoutSessionAsync(pendingCheckout.Id, business.Name, checkoutLines, successUrl, cancelUrl);

        pendingCheckout.StripeCheckoutSessionId = session.SessionId;
        await dbContext.SaveChangesAsync();

        return session.Url;
    }

    public async Task<CheckoutCompletionResult> CompleteCheckoutAsync(Guid pendingCheckoutId, string sessionId)
    {
        var pendingCheckout = await dbContext.PendingCheckouts.FirstOrDefaultAsync(p => p.Id == pendingCheckoutId);
        if (pendingCheckout is null)
            return new CheckoutCompletionResult(false, "This checkout session could not be found.", null, null);

        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId != pendingCheckout.UserId)
            throw new UnauthorizedAccessException("You can't complete someone else's checkout.");

        // Idempotent — a page refresh on the return URL replays this instead of double-spending
        // a single Stripe payment into two orders.
        if (pendingCheckout.ConsumedAt is not null)
        {
            if (pendingCheckout.ResultingOrderId is null)
                return new CheckoutCompletionResult(false, "That payment couldn't be turned into an order and was refunded.", null, null);

            var existingOrder = await LoadFullOrderAsync(pendingCheckout.ResultingOrderId.Value);
            return new CheckoutCompletionResult(true, "", existingOrder, existingOrder?.OrderPackages.Sum(op => op.Quantity * op.Package.WeightKg));
        }

        if (pendingCheckout.StripeCheckoutSessionId != sessionId)
            return new CheckoutCompletionResult(false, "This checkout session doesn't match.", null, null);

        var status = await stripeGateway.GetSessionStatusAsync(sessionId);
        if (!status.IsPaid)
            return new CheckoutCompletionResult(false, "Payment wasn't completed.", null, null);

        var lines = JsonSerializer.Deserialize<List<OrderLineRequest>>(pendingCheckout.LinesJson) ?? [];

        Order order;
        try
        {
            order = await orderService.PlaceOrderAsync(pendingCheckout.BusinessId, lines);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            // Payment went through but the order can't be placed (stock vanished, rate limit hit
            // in the time the customer spent on Stripe's page) — refund rather than keep the charge
            // for nothing.
            if (status.PaymentIntentId is not null)
                await stripeGateway.RefundAsync(status.PaymentIntentId);

            pendingCheckout.ConsumedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
            return new CheckoutCompletionResult(false, $"Payment succeeded but your order couldn't be placed ({ex.Message}) — you've been refunded.", null, null);
        }

        dbContext.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Amount = status.AmountTotal,
            Currency = status.Currency,
            StripeCheckoutSessionId = sessionId,
            StripePaymentIntentId = status.PaymentIntentId,
            Status = PaymentStatuses.Succeeded,
            CreatedAt = DateTime.UtcNow,
        });

        pendingCheckout.ConsumedAt = DateTime.UtcNow;
        pendingCheckout.ResultingOrderId = order.Id;
        await dbContext.SaveChangesAsync();

        var fullOrder = await LoadFullOrderAsync(order.Id);
        return new CheckoutCompletionResult(true, "", fullOrder, fullOrder?.OrderPackages.Sum(op => op.Quantity * op.Package.WeightKg));
    }

    public async Task<int> ExpireStalePendingCheckoutsAsync()
    {
        var cutoff = DateTime.UtcNow - OrderExpiry.PendingCheckoutTimeout;
        var stale = await dbContext.PendingCheckouts
            .Where(p => p.ConsumedAt == null && p.CreatedAt < cutoff)
            .ToListAsync();

        if (stale.Count == 0)
            return 0;

        dbContext.PendingCheckouts.RemoveRange(stale);
        await dbContext.SaveChangesAsync();
        return stale.Count;
    }

    private async Task<Order?> LoadFullOrderAsync(Guid orderId) =>
        await dbContext.Orders
            .Include(o => o.Business)
            .Include(o => o.OrderPackages)
            .ThenInclude(op => op.Package)
            .FirstOrDefaultAsync(o => o.Id == orderId);
}
