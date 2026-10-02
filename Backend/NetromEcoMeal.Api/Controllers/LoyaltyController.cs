using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/loyalty")]
[ApiController]
[Authorize]
public class LoyaltyController(ILoyaltyService loyaltyService) : ControllerBase
{
    [HttpGet("{businessId:guid}/mine")]
    public async Task<ActionResult<LoyaltyProgress?>> GetMyProgress(Guid businessId) =>
        Ok(await loyaltyService.GetMyProgressAsync(businessId));
}
