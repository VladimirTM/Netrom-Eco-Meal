using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class StreakController(IStreakService streakService) : ControllerBase
{
    public async Task<ActionResult<int>> GetMyStreakWeeksAsync()
    {
        return await streakService.GetMyStreakWeeksAsync();
    }
}
