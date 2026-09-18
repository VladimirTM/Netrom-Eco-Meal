using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// A real external HTTP endpoint, not called in-process — unlike every other controller here,
// it's not registered as a scoped service in Program.cs, and it's authenticated via X-Api-Key.
[ApiController]
[Route("/api/webhooks")]
[AllowAnonymous]
public class WebhookController(IWebhookIntakeService webhookIntakeService) : ControllerBase
{
    // Lets a business's own POS/inventory system create a live Package instead of a manager
    // retyping it into PackageForm.razor.
    [HttpPost("packages")]
    public async Task<ActionResult<WebhookPackageResponse>> CreatePackageAsync([FromHeader(Name = "X-Api-Key")] string? apiKey, [FromBody] WebhookPackageRequest request)
    {
        try
        {
            var package = await webhookIntakeService.CreatePackageAsync(apiKey, request);
            // Not the raw entity — see WebhookPackageResponse for why.
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
