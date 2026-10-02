using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/push-subscriptions")]
[ApiController]
public class PushSubscriptionsController(IPushSubscriptionService pushSubscriptionService) : ControllerBase
{
    [HttpGet("public-key")]
    [AllowAnonymous]
    public ActionResult<string?> GetPublicKey() => Ok(pushSubscriptionService.GetPublicKey());

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Subscribe([FromBody] SubscribePushRequestDto request)
    {
        await pushSubscriptionService.SubscribeAsync(request.Endpoint, request.P256dh, request.Auth);
        return NoContent();
    }

    [HttpPost("unsubscribe")]
    [Authorize]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribePushRequestDto request)
    {
        await pushSubscriptionService.UnsubscribeAsync(request.Endpoint);
        return NoContent();
    }
}
