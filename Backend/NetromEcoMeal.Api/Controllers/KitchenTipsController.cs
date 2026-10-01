using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/kitchen-tips")]
[ApiController]
public class KitchenTipsController(IKitchenTipService kitchenTipService) : ControllerBase
{
    [HttpGet("by-business/{businessId:guid}")]
    public async Task<ActionResult<List<KitchenTipDto>>> GetByBusiness(Guid businessId) =>
        Ok((await kitchenTipService.GetVisibleByBusinessIdAsync(businessId)).Select(KitchenTipDto.FromEntity).ToList());

    [HttpPost("{businessId:guid}")]
    [Authorize]
    public async Task<ActionResult<KitchenTipDto>> Submit(Guid businessId, [FromBody] SubmitKitchenTipRequestDto request) =>
        Ok(KitchenTipDto.FromEntity(await kitchenTipService.SubmitAsync(businessId, request.Tip)));
}
