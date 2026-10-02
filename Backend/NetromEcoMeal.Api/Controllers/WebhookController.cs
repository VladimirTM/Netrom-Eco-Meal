using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Models;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

// Ported from NetromEcoMeal.Web's WebhookController, which was already a real, external HTTP
// endpoint (X-Api-Key, not cookie/JWT auth) — moves across unchanged, local try/catch included,
// since it predates this phase's global ExceptionHandlingMiddleware and ArgumentException isn't
// one of that middleware's mapped cases. Same path/shape as Web's copy (D5) so a business's
// already-configured POS/inventory integration doesn't need to change its target.
[ApiController]
[Route("/api/webhooks")]
[AllowAnonymous]
public class WebhookController(IWebhookIntakeService webhookIntakeService) : ControllerBase
{
    [HttpPost("packages")]
    public async Task<ActionResult<WebhookPackageResponse>> CreatePackageAsync([FromHeader(Name = "X-Api-Key")] string? apiKey, [FromBody] WebhookPackageRequest request)
    {
        try
        {
            var package = await webhookIntakeService.CreatePackageAsync(apiKey, request);
            var response = new WebhookPackageResponse(package.Id, package.BusinessId, package.Name, package.Price, package.Quantity, package.WeightKg, package.PickupStart, package.PickupEnd);
            return Created($"/packages/{package.Id}", response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
