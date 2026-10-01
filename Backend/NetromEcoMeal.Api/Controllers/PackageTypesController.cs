using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/package-types")]
[ApiController]
public class PackageTypesController(IPackageTypeService packageTypeService) : ControllerBase
{
    // Public so a POS/inventory system can discover valid PackageTypeIds without a login.
    // [AllowAnonymous] here only, not class-level, or it would override [Authorize] on the writes below.
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<PackageTypeDto>>> GetAll() =>
        Ok((await packageTypeService.GetAllAsync()).Select(PackageTypeDto.FromEntity).ToList());

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Add([FromBody] PackageTypeWriteDto request)
    {
        var entity = request.ToEntity(Guid.NewGuid());
        await packageTypeService.AddAsync(entity);
        return CreatedAtAction(nameof(GetAll), PackageTypeDto.FromEntity(entity));
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] PackageTypeWriteDto request)
    {
        await packageTypeService.UpdateAsync(request.ToEntity(id));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        await packageTypeService.DeleteAsync(id);
        return NoContent();
    }
}
