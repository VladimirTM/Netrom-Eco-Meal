using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Models;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

// X-Api-Key auth, not JWT — external POS/inventory systems call this directly, so its path and
// response shape must not change. Keeps its own try/catch since ArgumentException isn't one of
// ExceptionHandlingMiddleware's cases.
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
