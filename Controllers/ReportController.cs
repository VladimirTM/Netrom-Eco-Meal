using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class ReportController(IReportService reportService) : ControllerBase
{
    public async Task<ActionResult> SubmitAsync(string targetType, Guid targetId, string reason)
    {
        try
        {
            await reportService.SubmitAsync(targetType, targetId, reason);
            return Created();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    public async Task<ActionResult<List<ReportView>>> GetOpenAsync()
    {
        try
        {
            return await reportService.GetOpenAsync();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    public async Task<ActionResult> DismissAsync(Guid reportId)
    {
        try
        {
            await reportService.DismissAsync(reportId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    public async Task<ActionResult> TakeActionAsync(Guid reportId, string actionReason)
    {
        try
        {
            await reportService.TakeActionAsync(reportId, actionReason);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }
}
