using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/streaks")]
[ApiController]
[Authorize]
public class StreaksController(IStreakService streakService) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<int>> GetMyStreakWeeks() =>
        Ok(await streakService.GetMyStreakWeeksAsync());
}
