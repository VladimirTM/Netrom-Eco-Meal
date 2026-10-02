using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/standing-orders")]
[ApiController]
[Authorize]
public class StandingOrdersController(IStandingOrderService standingOrderService) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<List<StandingOrderDto>>> GetMyStandingOrders() =>
        Ok((await standingOrderService.GetMyStandingOrdersAsync()).Select(StandingOrderDto.FromEntity).ToList());

    [HttpPost]
    public async Task<ActionResult<StandingOrderDto>> Create([FromBody] CreateStandingOrderRequestDto request)
    {
        var standingOrder = await standingOrderService.CreateAsync(request.BusinessId, request.PackageTypeId, request.DietaryTag, request.MaxWeeklySpend);
        return Ok(StandingOrderDto.FromEntity(standingOrder));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStandingOrderRequestDto request)
    {
        await standingOrderService.UpdateAsync(id, request.MaxWeeklySpend, request.IsActive);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await standingOrderService.DeleteAsync(id);
        return NoContent();
    }
}
