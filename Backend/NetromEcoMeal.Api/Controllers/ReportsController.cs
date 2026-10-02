using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/reports")]
[ApiController]
[Authorize]
public class ReportsController(IReportService reportService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitReportRequestDto request)
    {
        await reportService.SubmitAsync(request.TargetType, request.TargetId, request.Reason);
        return Created();
    }

    [HttpGet("open")]
    public async Task<ActionResult<List<ReportViewDto>>> GetOpen() =>
        Ok((await reportService.GetOpenAsync()).Select(ReportViewDto.FromModel).ToList());

    [HttpPost("{reportId:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid reportId)
    {
        await reportService.DismissAsync(reportId);
        return NoContent();
    }

    [HttpPost("{reportId:guid}/take-action")]
    public async Task<IActionResult> TakeAction(Guid reportId, [FromBody] TakeReportActionRequestDto request)
    {
        await reportService.TakeActionAsync(reportId, request.ActionReason);
        return NoContent();
    }
}
