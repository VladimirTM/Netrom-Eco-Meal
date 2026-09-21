using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services.Interfaces;
using Stripe;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class PaymentController(ICheckoutService checkoutService) : ControllerBase
{
    public async Task<ActionResult<string>> CreateCheckoutSessionAsync(Guid businessId, List<OrderLineRequest> lines, string? logisticsNote = null)
    {
        try
        {
            return await checkoutService.StartCheckoutAsync(businessId, lines, logisticsNote);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            return Conflict(ex.Message);
        }
        catch (StripeException)
        {
            // Belt-and-suspenders alongside Checkout.MinChargeableAmount — a raw Stripe error here
            // would otherwise crash the whole Blazor circuit instead of showing CartPanel's inline error.
            return Conflict("We couldn't start checkout for this order — please try a different basket total.");
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
