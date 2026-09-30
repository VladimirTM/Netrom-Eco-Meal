using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class ReferralController(IReferralService referralService) : ControllerBase
{
    public async Task<ActionResult<ReferralInfo>> GetMyReferralInfoAsync()
    {
        try
        {
            return await referralService.GetMyReferralInfoAsync();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }
}
