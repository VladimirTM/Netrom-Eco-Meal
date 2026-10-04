using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Constants;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Models;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/businesses")]
[ApiController]
public class BusinessesController(IBusinessService businessService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedList<BusinessDto>>> GetPaged(
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] Guid? businessTypeId = null, [FromQuery] string? staffUserId = null, [FromQuery] string? sortBy = null,
        [FromQuery] bool favoritesOnly = false, [FromQuery] double? customerLat = null, [FromQuery] double? customerLng = null,
        [FromQuery] string? statusFilter = null, [FromQuery] bool publicOnly = false, [FromQuery] string? dietaryTag = null,
        [FromQuery] decimal? maxPrice = null)
    {
        var page = await businessService.GetPagedAsync(pageIndex, pageSize, search, businessTypeId, staffUserId, sortBy,
            favoritesOnly, customerLat, customerLng, statusFilter, publicOnly, dietaryTag, maxPrice);
        return Ok(page.MapItems(BusinessDto.FromEntity));
    }

    [HttpGet("all")]
    public async Task<ActionResult<List<BusinessDto>>> GetAll([FromQuery] bool publicOnly = false) =>
        Ok((await businessService.GetAllAsync(publicOnly)).Select(BusinessDto.FromEntity).ToList());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BusinessDto>> GetById(Guid id)
    {
        var business = await businessService.GetByIdAsync(id);
        return business is null ? NotFound() : Ok(BusinessDto.FromEntity(business));
    }

    [HttpGet("{id:guid}/staff")]
    [Authorize]
    public async Task<ActionResult<List<StaffMemberDto>>> GetStaff(Guid id) =>
        Ok((await businessService.GetStaffAsync(id)).Select(u => new StaffMemberDto(u.Id, u.Name, u.Email!)).ToList());

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<BusinessDto>> Create([FromBody] BusinessWriteDto request)
    {
        var business = request.ToEntity(Guid.NewGuid());
        await businessService.AddAsync(business);
        return CreatedAtAction(nameof(GetById), new { id = business.Id }, BusinessDto.FromEntity(business));
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] BusinessWriteDto request)
    {
        await businessService.UpdateAsync(request.ToEntity(id));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var business = await businessService.GetByIdAsync(id);
        if (business is null)
            return NotFound();

        await businessService.DeleteAsync(business);
        return NoContent();
    }

    [HttpPost("apply")]
    [Authorize]
    public async Task<ActionResult<BusinessDto>> Apply([FromBody] BusinessWriteDto request)
    {
        var business = await businessService.ApplyAsync(request.ToEntity(Guid.NewGuid()));
        return CreatedAtAction(nameof(GetById), new { id = business.Id }, BusinessDto.FromEntity(business));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Approve(Guid id)
    {
        await businessService.ApproveAsync(id);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectBusinessRequestDto request)
    {
        await businessService.RejectAsync(id, request.Reason);
        return NoContent();
    }

    [HttpPost("{id:guid}/hide")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Hide(Guid id, [FromBody] HideRequestDto request)
    {
        var business = await businessService.HideAsync(id, request.Reason);
        return business is null ? NotFound() : NoContent();
    }

    [HttpPost("{id:guid}/unhide")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Unhide(Guid id)
    {
        await businessService.UnhideAsync(id);
        return NoContent();
    }

    [HttpPost("{id:guid}/staff")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> AddStaff(Guid id, [FromBody] AddStaffRequestDto request)
    {
        var added = await businessService.AddStaffAsync(id, request.UserId, request.UserName);
        return added ? NoContent() : Conflict();
    }

    [HttpDelete("{id:guid}/staff/{userId}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> RemoveStaff(Guid id, string userId)
    {
        var removed = await businessService.RemoveStaffAsync(id, userId);
        return removed ? NoContent() : NotFound();
    }

    [HttpPut("{id:guid}/hours")]
    [Authorize]
    public async Task<IActionResult> SetHours(Guid id, [FromBody] SetHoursRequestDto request)
    {
        await businessService.SetHoursAsync(id, request.ToEntities(id));
        return NoContent();
    }

    [HttpPost("{id:guid}/closures")]
    [Authorize]
    public async Task<ActionResult<BusinessClosureDto>> AddClosure(Guid id, [FromBody] AddClosureRequestDto request)
    {
        var closure = await businessService.AddClosureAsync(id, request.StartDate, request.EndDate, request.Reason);
        return Ok(new BusinessClosureDto(closure.Id, closure.StartDate, closure.EndDate, closure.Reason));
    }

    [HttpDelete("{id:guid}/closures/{closureId:guid}")]
    [Authorize]
    public async Task<IActionResult> RemoveClosure(Guid id, Guid closureId)
    {
        var removed = await businessService.RemoveClosureAsync(id, closureId);
        return removed ? NoContent() : NotFound();
    }

    // Returns the plaintext key — the caller is expected to show it exactly once (same contract
    // as today's Dashboard.tsx).
    [HttpPost("{id:guid}/webhook-key")]
    [Authorize]
    public async Task<ActionResult<string>> GenerateApiKey(Guid id)
    {
        var key = await businessService.GenerateApiKeyAsync(id);
        return key is null ? NotFound() : Ok(key);
    }

    [HttpDelete("{id:guid}/webhook-key")]
    [Authorize]
    public async Task<IActionResult> RevokeApiKey(Guid id)
    {
        await businessService.RevokeApiKeyAsync(id);
        return NoContent();
    }
}
