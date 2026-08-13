using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class PaymentController(ICheckoutService checkoutService) : ControllerBase
{
    public async Task<ActionResult<string>> CreateCheckoutSessionAsync(Guid businessId, List<OrderLineRequest> lines)
    {
        try
        {
            return await checkoutService.StartCheckoutAsync(businessId, lines);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            return Conflict(ex.Message);
        }
    }

    public async Task<ActionResult<CheckoutCompletionResult>> CompleteCheckoutAsync(Guid pendingCheckoutId, string sessionId)
    {
        try
        {
            return await checkoutService.CompleteCheckoutAsync(pendingCheckoutId, sessionId);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }
}
