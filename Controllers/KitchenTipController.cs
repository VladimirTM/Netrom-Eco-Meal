using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class KitchenTipController(IKitchenTipService kitchenTipService) : ControllerBase
{
    public async Task<ActionResult<List<KitchenTip>>> GetByBusinessAsync(Guid businessId)
    {
        return await kitchenTipService.GetVisibleByBusinessIdAsync(businessId);
    }

    public async Task<ActionResult> SubmitAsync(Guid businessId, string tip)
    {
        try
        {
            await kitchenTipService.SubmitAsync(businessId, tip);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Conflict(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }
}
