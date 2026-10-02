using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Models;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/impact")]
[ApiController]
public class ImpactController(IImpactService impactService) : ControllerBase
{
    // Public and cross-origin: called from a business's own website via wwwroot/js/impact-widget.js.
    [HttpGet("/api/businesses/{businessId:guid}/impact")]
    [AllowAnonymous]
    [EnableCors("PublicImpactWidget")]
    public async Task<ActionResult<BusinessImpactWidgetDto>> GetBusinessWidget(Guid businessId)
    {
        var stats = await impactService.GetBusinessWidgetStatsAsync(businessId);
        return stats is null ? NotFound() : Ok(stats);
    }

    [HttpGet("leaderboard")]
    [AllowAnonymous]
    public async Task<ActionResult<List<LeaderboardEntry>>> GetMonthlyLeaderboard([FromQuery] int take = 20) =>
        Ok(await impactService.GetMonthlyLeaderboardAsync(take));

    [HttpGet("opt-in/mine")]
    [Authorize]
    public async Task<ActionResult<bool>> GetMyOptInStatus() =>
        Ok(await impactService.GetMyOptInStatusAsync());

    [HttpPut("opt-in/mine")]
    [Authorize]
    public async Task<IActionResult> SetMyOptInStatus([FromBody] SetImpactOptInRequestDto request)
    {
        await impactService.SetMyOptInStatusAsync(request.ShowOnLeaderboard);
        return NoContent();
    }
}
