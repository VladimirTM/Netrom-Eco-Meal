using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/payments")]
[ApiController]
[Authorize]
public class PaymentsController(ICheckoutService checkoutService) : ControllerBase
{
    // Returns the Stripe Checkout URL to redirect the browser to; success_url/cancel_url
    // point back at the frontend's origin.
    [HttpPost("checkout-session")]
    public async Task<ActionResult<string>> CreateCheckoutSession([FromBody] StartCheckoutRequestDto request) =>
        Ok(await checkoutService.StartCheckoutAsync(request.BusinessId, request.Lines, request.LogisticsNote));

    [HttpPost("complete")]
    public async Task<ActionResult<CheckoutCompletionResponseDto>> CompleteCheckout([FromBody] CompleteCheckoutRequestDto request) =>
        Ok(CheckoutCompletionResponseDto.FromModel(await checkoutService.CompleteCheckoutAsync(request.PendingCheckoutId, request.SessionId)));
}
