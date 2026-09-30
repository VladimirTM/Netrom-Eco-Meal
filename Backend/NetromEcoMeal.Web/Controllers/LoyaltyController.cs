using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class LoyaltyController(ILoyaltyService loyaltyService) : ControllerBase
{
    public async Task<ActionResult<LoyaltyProgress?>> GetMyProgressAsync(Guid businessId)
    {
        return await loyaltyService.GetMyProgressAsync(businessId);
    }
}
