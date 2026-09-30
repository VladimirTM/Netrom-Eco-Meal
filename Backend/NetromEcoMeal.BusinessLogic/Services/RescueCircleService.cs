using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Database;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Services;

public class RescueCircleService(
    IOrderService orderService,
    IBusinessService businessService,
    IPackageRepository packageRepository,
    IStripeGateway stripeGateway,
    INotificationService notificationService,
    EcoMealDbContext dbContext,
    ICurrentUser currentUser,
    IConfiguration configuration) : IRescueCircleService
{
    private string BaseUrl => (configuration["App:BaseUrl"] ?? "http://localhost:8080").TrimEnd('/');

    public async Task<string> StartCircleAsync(Guid businessId, List<OrderLineRequest> lines, int participantCount, string? logisticsNote = null)
    {
        if (participantCount is < RescueCircles.MinParticipants or > RescueCircles.MaxParticipants)
            throw new InvalidOperationException($"A Rescue Circle needs between {RescueCircles.MinParticipants} and {RescueCircles.MaxParticipants} people.");

        // Priced and validated before PlaceOrderAsync commits anything — a share this small used
        // to only fail once StartShareCheckoutAsync hit Stripe, leaving a real order and an Open
        // circle nobody could ever pay off.
        var packageIds = lines.Select(l => l.PackageId).Distinct().ToList();
        var packages = (await packageRepository.GetByIdsAsync(packageIds)).ToDictionary(p => p.Id);
        var totalAmount = lines.Sum(l => packages.TryGetValue(l.PackageId, out var pkg) ? pkg.Price * l.Quantity : 0);

        var otherShare = BaseShare(totalAmount, participantCount);
        var organizerShare = totalAmount - otherShare * (participantCount - 1);
        if (otherShare < Checkout.MinChargeableAmount || organizerShare < Checkout.MinChargeableAmount)
            throw new InvalidOperationException("This order can't be split that many ways — try fewer participants or a larger basket.");

        // Reuses every existing rule PlaceOrderAsync already enforces (customer-only, rate limit,
        // stock availability) — the only difference from a solo order is that nobody's paid yet.
        var order = await orderService.PlaceOrderAsync(businessId, lines, logisticsNote);

        var (_, organizerId) = await currentUser.GetCurrentUserAsync();

        var circle = new RescueCircle
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            OrganizerId = organizerId!,
            ParticipantCount = participantCount,
            TotalAmount = totalAmount,
            Status = RescueCircleStatuses.Open,
            CreatedAt = DateTime.UtcNow,
        };
        dbContext.RescueCircles.Add(circle);

        // otherShare/organizerShare priced above — the organizer's slot absorbs the rounding
        // remainder, so every other share is a clean, round-cent amount.
        var organizerParticipant = new RescueCircleParticipant
        {
            Id = Guid.NewGuid(),
            RescueCircleId = circle.Id,
            UserId = organizerId!,
            ShareAmount = organizerShare,
            JoinedAt = DateTime.UtcNow,
        };
        dbContext.RescueCircleParticipants.Add(organizerParticipant);
        await dbContext.SaveChangesAsync();

        var business = await businessService.GetByIdAsync(businessId)
            ?? throw new InvalidOperationException("This business is no longer available.");

        return await StartShareCheckoutAsync(circle, organizerParticipant, business);
    }

    public async Task<string> JoinOrPayAsync(Guid circleId)
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to join a Rescue Circle.");

        var circle = await dbContext.RescueCircles.Include(c => c.Participants)
            .Include(c => c.Order).ThenInclude(o => o.Business)
            .FirstOrDefaultAsync(c => c.Id == circleId)
            ?? throw new InvalidOperationException("This Rescue Circle no longer exists.");

        if (circle.Status != RescueCircleStatuses.Open)
            throw new InvalidOperationException("This Rescue Circle is no longer open.");

        var business = circle.Order.Business;

        var existing = circle.Participants.FirstOrDefault(p => p.UserId == userId);
        if (existing is not null)
        {
            if (existing.PaidAt is not null)
                throw new InvalidOperationException("You've already paid your share.");

            return await StartShareCheckoutAsync(circle, existing, business);
        }

        if (circle.Participants.Count >= circle.ParticipantCount)
            throw new InvalidOperationException("This Rescue Circle is already full.");

        var participant = new RescueCircleParticipant
        {
            Id = Guid.NewGuid(),
            RescueCircleId = circle.Id,
            UserId = userId,
            ShareAmount = BaseShare(circle.TotalAmount, circle.ParticipantCount),
            JoinedAt = DateTime.UtcNow,
        };
        dbContext.RescueCircleParticipants.Add(participant);
        await dbContext.SaveChangesAsync();

        return await StartShareCheckoutAsync(circle, participant, business);
    }

    public async Task<RescueCircleCompletionResult> CompleteShareCheckoutAsync(Guid circleId, Guid participantId, string sessionId)
    {
        var circle = await dbContext.RescueCircles.Include(c => c.Participants).ThenInclude(p => p.User)
            .Include(c => c.Order).ThenInclude(o => o.Business)
            .FirstOrDefaultAsync(c => c.Id == circleId);
        if (circle is null)
            return new RescueCircleCompletionResult(false, "This Rescue Circle no longer exists.", null);

        var participant = circle.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null)
            return new RescueCircleCompletionResult(false, "This share couldn't be found.", null);

        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (participant.UserId != userId)
            throw new UnauthorizedAccessException("You can't complete someone else's share.");

        // Idempotent — a page refresh on the return URL replays this instead of re-verifying (and
        // potentially double-notifying) a payment that already went through.
        if (participant.PaidAt is not null)
            return new RescueCircleCompletionResult(true, "", await BuildSummaryAsync(circle, userId));

        if (participant.StripeCheckoutSessionId != sessionId)
            return new RescueCircleCompletionResult(false, "This checkout session doesn't match.", null);

        var status = await stripeGateway.GetSessionStatusAsync(sessionId);
        if (!status.IsPaid)
            return new RescueCircleCompletionResult(false, "Payment wasn't completed.", null);

        participant.PaidAt = DateTime.UtcNow;
        participant.StripePaymentIntentId = status.PaymentIntentId;

        var fullyPaid = circle.Participants.Count == circle.ParticipantCount && circle.Participants.All(p => p.PaidAt is not null);
        if (fullyPaid)
        {
            // A summary Payment row so Payments.razor/OrderDetailModal/the CSV export show this
            // order as paid without needing their own Rescue Circle awareness — the actual charges
            // are the per-participant ones above; this one carries no Stripe ids of its own, which
            // is also how OrderService.RefundIfPaidAsync tells a circle order apart from a solo one.
            dbContext.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = circle.OrderId,
                Amount = circle.TotalAmount,
                Currency = status.Currency,
                StripeCheckoutSessionId = $"circle_{circle.Id:N}",
                Status = PaymentStatuses.Succeeded,
                CreatedAt = DateTime.UtcNow,
            });

            await notificationService.CreateAsync(circle.OrganizerId,
                $"Your Rescue Circle at {circle.Order.Business.Name} is fully paid — it's now waiting on the kitchen to confirm.",
                "/orders");
        }

        await dbContext.SaveChangesAsync();

        return new RescueCircleCompletionResult(true, "", await BuildSummaryAsync(circle, userId));
    }

    public async Task<RescueCircleSummary> GetSummaryAsync(Guid circleId)
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to view a Rescue Circle.");

        var circle = await dbContext.RescueCircles.Include(c => c.Participants)
            .Include(c => c.Order).ThenInclude(o => o.Business)
            .FirstOrDefaultAsync(c => c.Id == circleId)
            ?? throw new InvalidOperationException("This Rescue Circle no longer exists.");

        return await BuildSummaryAsync(circle, userId);
    }

    public async Task<List<RescueCircleSummary>> GetMyCirclesAsync()
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to view your Rescue Circles.");

        var circles = await dbContext.RescueCircles.Include(c => c.Participants)
            .Include(c => c.Order).ThenInclude(o => o.Business)
            .Where(c => c.OrganizerId == userId || c.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var summaries = new List<RescueCircleSummary>();
        foreach (var circle in circles)
            summaries.Add(await BuildSummaryAsync(circle, userId));

        return summaries;
    }

    public async Task<(RescueCircle Circle, RescueCircleSummary Summary)> GetDetailAsync(Guid circleId)
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to view a Rescue Circle.");

        var circle = await dbContext.RescueCircles.Include(c => c.Participants).ThenInclude(p => p.User)
            .Include(c => c.Order).ThenInclude(o => o.Business)
            .FirstOrDefaultAsync(c => c.Id == circleId)
            ?? throw new InvalidOperationException("This Rescue Circle no longer exists.");

        if (circle.OrganizerId != userId && circle.Participants.All(p => p.UserId != userId))
            throw new UnauthorizedAccessException("Only the organizer or a participant can see who's paid.");

        return (circle, await BuildSummaryAsync(circle, userId));
    }

    public async Task LeaveCircleAsync(Guid circleId)
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to leave a Rescue Circle.");

        var circle = await dbContext.RescueCircles.Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == circleId)
            ?? throw new InvalidOperationException("This Rescue Circle no longer exists.");

        if (circle.OrganizerId == userId)
            throw new InvalidOperationException("As the organizer, cancel your order instead of leaving — that refunds everyone who's paid.");

        var participant = circle.Participants.FirstOrDefault(p => p.UserId == userId)
            ?? throw new InvalidOperationException("You're not part of this Rescue Circle.");

        var fullyPaid = circle.Participants.Count == circle.ParticipantCount && circle.Participants.All(p => p.PaidAt is not null);
        if (fullyPaid)
            throw new InvalidOperationException("This Rescue Circle is already fully paid — it's too late to back out.");

        if (participant.PaidAt is not null && participant.RefundedAt is null)
        {
            await stripeGateway.RefundAsync(participant.StripePaymentIntentId!);
            participant.RefundedAt = DateTime.UtcNow;
        }

        // Removed, not just marked — frees the slot for a fresh joiner at the same share amount.
        dbContext.RescueCircleParticipants.Remove(participant);
        await dbContext.SaveChangesAsync();
    }

    // Every non-organizer slot gets this exact, round-cent amount; the organizer's own share (set
    // once, at creation) absorbs whatever a plain division wouldn't divide evenly.
    private static decimal BaseShare(decimal total, int participantCount) =>
        Math.Floor(total / participantCount * 100m) / 100m;

    private async Task<string> StartShareCheckoutAsync(RescueCircle circle, RescueCircleParticipant participant, Business business)
    {
        var successUrl = $"{BaseUrl}/circles/return?circle={circle.Id}&participant={participant.Id}&session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{BaseUrl}/circles/{circle.Id}";

        // business.Name is passed to CreateCheckoutSessionAsync below and already appears as the
        // merchant name on Stripe's own checkout page — repeating it here doubled it up.
        var lineItem = new CheckoutLineItem("Rescue Circle share", participant.ShareAmount, 1);
        var session = await stripeGateway.CreateCheckoutSessionAsync(participant.Id, business.Name, [lineItem], successUrl, cancelUrl);

        participant.StripeCheckoutSessionId = session.SessionId;
        await dbContext.SaveChangesAsync();

        return session.Url;
    }

    private Task<RescueCircleSummary> BuildSummaryAsync(RescueCircle circle, string? viewerUserId)
    {
        var joined = circle.Participants.Count;
        var paid = circle.Participants.Count(p => p.PaidAt is not null);
        var mine = circle.Participants.FirstOrDefault(p => p.UserId == viewerUserId);

        return Task.FromResult(new RescueCircleSummary(
            circle.Id, circle.Order.BusinessId, circle.Order.Business.Name, circle.Status,
            circle.ParticipantCount, joined, paid, circle.TotalAmount,
            mine?.ShareAmount ?? BaseShare(circle.TotalAmount, circle.ParticipantCount),
            mine is not null));
    }
}
