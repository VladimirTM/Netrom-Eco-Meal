using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services.Interfaces;
using Stripe;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP —
// same convention as PaymentController.
[ApiController]
[Route("/")]
public class RescueCircleController(IRescueCircleService rescueCircleService) : ControllerBase
{
    public async Task<ActionResult<string>> StartCircleAsync(Guid businessId, List<OrderLineRequest> lines, int participantCount, string? logisticsNote = null)
    {
        try
        {
            return await rescueCircleService.StartCircleAsync(businessId, lines, participantCount, logisticsNote);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            return Conflict(ex.Message);
        }
        catch (StripeException)
        {
            // Belt-and-suspenders alongside RescueCircleService's own upfront share-size check —
            // same crash risk as PaymentController's checkout for any other Stripe failure.
            return Conflict("This order can't be split that many ways — try fewer participants or a larger basket.");
        }
    }

    public async Task<ActionResult<string>> JoinOrPayAsync(Guid circleId)
    {
        try
        {
            return await rescueCircleService.JoinOrPayAsync(circleId);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            return Conflict(ex.Message);
        }
        catch (StripeException)
        {
            return Conflict("We couldn't start payment for your share — please contact the organizer.");
        }
    }

    public async Task<ActionResult<RescueCircleCompletionResult>> CompleteShareCheckoutAsync(Guid circleId, Guid participantId, string sessionId)
    {
        try
        {
            return await rescueCircleService.CompleteShareCheckoutAsync(circleId, participantId, sessionId);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    public async Task<ActionResult<RescueCircleSummary>> GetSummaryAsync(Guid circleId)
    {
        try
        {
            return await rescueCircleService.GetSummaryAsync(circleId);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            return Conflict(ex.Message);
        }
    }

    public async Task<ActionResult<List<RescueCircleSummary>>> GetMyCirclesAsync()
    {
        try
        {
            return await rescueCircleService.GetMyCirclesAsync();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    public async Task<ActionResult<RescueCircleDetail>> GetDetailAsync(Guid circleId)
    {
        try
        {
            var (circle, summary) = await rescueCircleService.GetDetailAsync(circleId);
            return new RescueCircleDetail(summary, circle.OrganizerId, circle.Participants
                .OrderBy(p => p.JoinedAt)
                .Select(p => new RescueCircleParticipantView(p.UserId, p.User.Name, p.UserId == circle.OrganizerId, p.ShareAmount, p.JoinedAt, p.PaidAt, p.RefundedAt))
                .ToList());
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            return Conflict(ex.Message);
        }
    }

    public async Task<ActionResult> LeaveCircleAsync(Guid circleId)
    {
        try
        {
            await rescueCircleService.LeaveCircleAsync(circleId);
            return Ok();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            return Conflict(ex.Message);
        }
    }
}

// Flattened for the invite/detail page — RescueCircleService.GetDetailAsync itself returns the
// tracked entity, which Razor pages shouldn't hold onto past the request.
public record RescueCircleParticipantView(string UserId, string UserName, bool IsOrganizer, decimal ShareAmount, DateTime JoinedAt, DateTime? PaidAt, DateTime? RefundedAt);
public record RescueCircleDetail(RescueCircleSummary Summary, string OrganizerId, List<RescueCircleParticipantView> Participants);
