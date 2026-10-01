using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/business-types")]
[ApiController]
public class BusinessTypesController(IBusinessTypeService businessTypeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BusinessTypeDto>>> GetAll() =>
        Ok((await businessTypeService.GetAllAsync()).Select(BusinessTypeDto.FromEntity).ToList());

    // Admin-only; the service itself enforces that via ICurrentUser.EnsureAdminAsync (mapped to
    // 403 by the global exception middleware), the same pattern as every other lookup controller.
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Add([FromBody] BusinessTypeWriteDto request)
    {
        var entity = request.ToEntity(Guid.NewGuid());
        await businessTypeService.AddAsync(entity);
        return CreatedAtAction(nameof(GetAll), BusinessTypeDto.FromEntity(entity));
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] BusinessTypeWriteDto request)
    {
        await businessTypeService.UpdateAsync(request.ToEntity(id));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        await businessTypeService.DeleteAsync(id);
        return NoContent();
    }
}
