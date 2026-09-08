using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

public record RescueCircleCompletionResult(bool Success, string Message, RescueCircleSummary? Summary);

// Enough to decide "should I join?" without exposing who else has, or their names — shown to any
// signed-in customer who opens the invite link, participant or not. See RescueCircleService.GetDetailAsync
// for the fuller "who's paid, who still owes" view restricted to the organizer/existing participants.
public record RescueCircleSummary(
    Guid Id, Guid BusinessId, string BusinessName, string Status,
    int ParticipantCount, int JoinedCount, int PaidCount, decimal TotalAmount, decimal ShareAmount, bool IsMine);

// Owns the "shared basket, split payment" flow: StartCircleAsync places one Order the way
// PlaceOrderAsync always has (reserving stock, notifying staff) but — unlike a solo checkout —
// before anyone's actually paid, since a group needs time to collect everyone's share against it.
// Each participant (the organizer included) then pays their own ShareAmount through their own
// Stripe Checkout session; OrderService.ApplyStatusChangeAsync's Pending->Confirmed gate refuses to
// let a manager confirm the order until every slot is paid.
public interface IRescueCircleService
{
    // Customer-only (becomes the organizer). Reuses IOrderService.PlaceOrderAsync's own auth/stock/
    // rate-limit rules for the underlying Order, then starts the organizer's own share checkout.
    Task<string> StartCircleAsync(Guid businessId, List<OrderLineRequest> lines, int participantCount);
    // Customer-only. Joins an Open, not-yet-full circle and starts that participant's own share
    // checkout — or, if they already joined but haven't paid, just restarts their own checkout.
    Task<string> JoinOrPayAsync(Guid circleId);
    // Called from the Stripe return page for either the organizer's or a joiner's share. Idempotent
    // on repeat visits the same way CheckoutService.CompleteCheckoutAsync is.
    Task<RescueCircleCompletionResult> CompleteShareCheckoutAsync(Guid circleId, Guid participantId, string sessionId);
    // Any signed-in customer — backs the invite link's "join?" decision.
    Task<RescueCircleSummary> GetSummaryAsync(Guid circleId);
    // Every circle the current customer organizes or has joined, newest first — backs /circles.
    Task<List<RescueCircleSummary>> GetMyCirclesAsync();
    // Organizer or an existing participant only — the full per-person paid/unpaid breakdown.
    Task<(RescueCircle Circle, RescueCircleSummary Summary)> GetDetailAsync(Guid circleId);
    // A non-organizer participant backing out before the circle is fully paid — refunds only their
    // own share (if already paid) and frees their slot for someone else. The organizer instead
    // cancels the whole circle via the existing IOrderService.CancelMyOrderAsync on its Order.
    Task LeaveCircleAsync(Guid circleId);
}
