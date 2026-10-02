using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/referrals")]
[ApiController]
[Authorize]
public class ReferralsController(IReferralService referralService) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<ReferralInfo>> GetMyReferralInfo() =>
        Ok(await referralService.GetMyReferralInfoAsync());
}
