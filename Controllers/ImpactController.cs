using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class ImpactController(IImpactService impactService) : ControllerBase
{
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
