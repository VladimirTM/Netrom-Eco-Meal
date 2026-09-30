using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Models;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class ImpactController(IImpactService impactService) : ControllerBase
{
    // The one action here meant to be a real cross-origin HTTP request — from a business's own
    // website via wwwroot/js/impact-widget.js — rather than DI'd in-process like the rest below.
    [HttpGet("/api/businesses/{businessId:guid}/impact")]
    [AllowAnonymous]
    [EnableCors("PublicImpactWidget")]
    public async Task<ActionResult<BusinessImpactWidgetDto>> GetBusinessWidgetAsync(Guid businessId)
    {
        var stats = await impactService.GetBusinessWidgetStatsAsync(businessId);
        return stats is null ? NotFound() : stats;
    }

    public async Task<ActionResult<List<LeaderboardEntry>>> GetMonthlyLeaderboardAsync(int take = 20)
    {
        return await impactService.GetMonthlyLeaderboardAsync(take);
    }

    public async Task<ActionResult<bool>> GetMyOptInStatusAsync()
    {
        return await impactService.GetMyOptInStatusAsync();
    }

    public async Task<ActionResult> SetMyOptInStatusAsync(bool showOnLeaderboard)
    {
        try
        {
            await impactService.SetMyOptInStatusAsync(showOnLeaderboard);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }
}
