using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/package-templates")]
[ApiController]
[Authorize]
public class PackageTemplatesController(IPackageTemplateService templateService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PackageTemplateDto>>> GetAll() =>
        Ok((await templateService.GetAllAsync()).Select(PackageTemplateDto.FromEntity).ToList());

    [HttpGet("by-business/{businessId:guid}")]
    public async Task<ActionResult<List<PackageTemplateDto>>> GetByBusiness(Guid businessId) =>
        Ok((await templateService.GetByBusinessIdAsync(businessId)).Select(PackageTemplateDto.FromEntity).ToList());

    [HttpPost]
    public async Task<ActionResult<PackageTemplateDto>> Create([FromBody] CreateTemplateRequestDto request)
    {
        var template = await templateService.CreateFromPackageAsync(request.PackageId, request.PickupStartTimeUtc, request.PickupEndTimeUtc);
        return Ok(PackageTemplateDto.FromEntity(template));
    }

    [HttpPut("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetTemplateActiveRequestDto request)
    {
        await templateService.SetActiveAsync(id, request.IsActive);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await templateService.DeleteAsync(id);
        return NoContent();
    }
}
