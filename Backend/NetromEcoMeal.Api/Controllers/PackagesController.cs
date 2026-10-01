using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Models;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/packages")]
[ApiController]
public class PackagesController(IPackageService packageService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PackageDto>>> GetAll() =>
        Ok((await packageService.GetAllAsync()).Select(PackageDto.FromEntity).ToList());

    [HttpGet("paged")]
    public async Task<ActionResult<PaginatedList<PackageDto>>> GetPaged(
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] Guid? businessId = null, [FromQuery] Guid? packageTypeId = null)
    {
        var page = await packageService.GetPagedAsync(pageIndex, pageSize, search, businessId, packageTypeId);
        return Ok(page.MapItems(PackageDto.FromEntity));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PackageDto>> GetById(Guid id)
    {
        var package = await packageService.GetByIdAsync(id);
        return package is null ? NotFound() : Ok(PackageDto.FromEntity(package));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<PackageDto>> Create([FromBody] PackageWriteDto request)
    {
        var package = request.ToEntity(Guid.NewGuid());
        await packageService.AddAsync(package);
        return CreatedAtAction(nameof(GetById), new { id = package.Id }, PackageDto.FromEntity(package));
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] PackageWriteDto request)
    {
        await packageService.UpdateAsync(request.ToEntity(id));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        var package = await packageService.GetByIdAsync(id);
        if (package is null)
            return NotFound();

        await packageService.DeleteAsync(package);
        return NoContent();
    }

    [HttpPost("bulk/duplicate")]
    [Authorize]
    public async Task<ActionResult<List<PackageDto>>> DuplicateMany([FromBody] List<Guid> packageIds) =>
        Ok((await packageService.DuplicateManyAsync(packageIds)).Select(PackageDto.FromEntity).ToList());

    [HttpPost("bulk/adjust-quantity")]
    [Authorize]
    public async Task<IActionResult> AdjustQuantityMany([FromBody] AdjustQuantityManyRequestDto request)
    {
        await packageService.AdjustQuantityManyAsync(request.PackageIds, request.Delta);
        return NoContent();
    }

    [HttpPost("bulk/extend-pickup")]
    [Authorize]
    public async Task<IActionResult> ExtendPickupWindowMany([FromBody] ExtendPickupWindowManyRequestDto request)
    {
        await packageService.ExtendPickupWindowManyAsync(request.PackageIds, TimeSpan.FromHours(request.Hours));
        return NoContent();
    }

    [HttpGet("analytics")]
    [Authorize]
    public async Task<ActionResult<List<PackageDto>>> GetForAnalytics([FromQuery] Guid? businessId, [FromQuery] DateTime since) =>
        Ok((await packageService.GetForAnalyticsAsync(businessId, since)).Select(PackageDto.FromEntity).ToList());

    [HttpGet("markdown-candidates")]
    [Authorize]
    public async Task<ActionResult<List<PackageDto>>> GetMarkdownCandidates([FromQuery] Guid? businessId) =>
        Ok((await packageService.GetMarkdownCandidatesAsync(businessId)).Select(PackageDto.FromEntity).ToList());

    [HttpGet("{id:guid}/markdown-suggestion")]
    [Authorize]
    public async Task<ActionResult<MarkdownSuggestionDto?>> GetMarkdownSuggestion(Guid id, CancellationToken cancellationToken)
    {
        var suggestion = await packageService.GetMarkdownSuggestionAsync(id, cancellationToken);
        return Ok(suggestion is null ? null : new MarkdownSuggestionDto(suggestion.CurrentPrice, suggestion.SuggestedPrice, suggestion.Explanation));
    }

    [HttpPost("{id:guid}/markdown-suggestion/dismiss")]
    [Authorize]
    public async Task<IActionResult> DismissMarkdownSuggestion(Guid id)
    {
        await packageService.DismissMarkdownSuggestionAsync(id);
        return NoContent();
    }

    [HttpGet("donation-candidates")]
    [Authorize]
    public async Task<ActionResult<List<PackageDto>>> GetDonationCandidates([FromQuery] Guid? businessId) =>
        Ok((await packageService.GetDonationCandidatesAsync(businessId)).Select(PackageDto.FromEntity).ToList());

    [HttpPost("{id:guid}/donate")]
    [Authorize]
    public async Task<ActionResult<PackageDto>> MarkAsDonated(Guid id)
    {
        var package = await packageService.MarkAsDonatedAsync(id);
        return package is null ? NotFound() : Ok(PackageDto.FromEntity(package));
    }

    [HttpPost("{id:guid}/hide")]
    [Authorize]
    public async Task<IActionResult> Hide(Guid id, [FromBody] HideRequestDto request)
    {
        var package = await packageService.HideAsync(id, request.Reason);
        return package is null ? NotFound() : NoContent();
    }

    [HttpPost("{id:guid}/unhide")]
    [Authorize]
    public async Task<IActionResult> Unhide(Guid id)
    {
        await packageService.UnhideAsync(id);
        return NoContent();
    }
}
