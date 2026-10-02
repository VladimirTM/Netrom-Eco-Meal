using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/rescue-circles")]
[ApiController]
[Authorize]
public class RescueCirclesController(IRescueCircleService rescueCircleService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<string>> StartCircle([FromBody] StartRescueCircleRequestDto request) =>
        Ok(await rescueCircleService.StartCircleAsync(request.BusinessId, request.Lines, request.ParticipantCount, request.LogisticsNote));

    [HttpPost("{circleId:guid}/join-or-pay")]
    public async Task<ActionResult<string>> JoinOrPay(Guid circleId) =>
        Ok(await rescueCircleService.JoinOrPayAsync(circleId));

    [HttpPost("{circleId:guid}/complete-share")]
    public async Task<ActionResult<RescueCircleCompletionResponseDto>> CompleteShareCheckout(Guid circleId, [FromBody] CompleteRescueCircleShareRequestDto request) =>
        Ok(RescueCircleCompletionResponseDto.FromModel(
            await rescueCircleService.CompleteShareCheckoutAsync(circleId, request.ParticipantId, request.SessionId)));

    [HttpGet("{circleId:guid}/summary")]
    public async Task<ActionResult<RescueCircleSummary>> GetSummary(Guid circleId) =>
        Ok(await rescueCircleService.GetSummaryAsync(circleId));

    [HttpGet("mine")]
    public async Task<ActionResult<List<RescueCircleSummary>>> GetMyCircles() =>
        Ok(await rescueCircleService.GetMyCirclesAsync());

    [HttpGet("{circleId:guid}")]
    public async Task<ActionResult<RescueCircleDetailDto>> GetDetail(Guid circleId)
    {
        var (circle, summary) = await rescueCircleService.GetDetailAsync(circleId);
        return Ok(new RescueCircleDetailDto(summary, circle.OrganizerId, circle.Participants
            .OrderBy(p => p.JoinedAt)
            .Select(p => new RescueCircleParticipantDto(p.UserId, p.User.Name, p.UserId == circle.OrganizerId, p.ShareAmount, p.JoinedAt, p.PaidAt, p.RefundedAt))
            .ToList()));
    }

    [HttpPost("{circleId:guid}/leave")]
    public async Task<IActionResult> LeaveCircle(Guid circleId)
    {
        await rescueCircleService.LeaveCircleAsync(circleId);
        return NoContent();
    }
}
