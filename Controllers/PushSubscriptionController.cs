using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class PushSubscriptionController(IPushSubscriptionService pushSubscriptionService) : ControllerBase
{
    public ActionResult<string?> GetPublicKey()
    {
        return pushSubscriptionService.GetPublicKey();
    }

    public async Task<ActionResult> SubscribeAsync(string endpoint, string p256dh, string auth)
    {
        try
        {
            await pushSubscriptionService.SubscribeAsync(endpoint, p256dh, auth);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Conflict(ex.Message);
        }
    }

    public async Task<ActionResult> UnsubscribeAsync(string endpoint)
    {
        await pushSubscriptionService.UnsubscribeAsync(endpoint);
        return NoContent();
    }
}
