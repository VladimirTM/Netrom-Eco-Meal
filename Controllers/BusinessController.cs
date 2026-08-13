using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class BusinessController(IBusinessService businessService) : ControllerBase
{
    public async Task<ActionResult<List<Business>>> GetAllAsync(bool publicOnly = false)
    {
        return await businessService.GetAllAsync(publicOnly);
    }

    public async Task<ActionResult<PaginatedList<Business>>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessTypeId, string? staffUserId = null, string? sortBy = null, bool favoritesOnly = false, double? customerLat = null, double? customerLng = null, string? statusFilter = null, bool publicOnly = false, string? dietaryTag = null)
    {
        return await businessService.GetPagedAsync(pageIndex, pageSize, search, businessTypeId, staffUserId, sortBy, favoritesOnly, customerLat, customerLng, statusFilter, publicOnly, dietaryTag);
    }

    public async Task<ActionResult<Business?>> GetByIdAsync(Guid id)
    {
        return await businessService.GetByIdAsync(id);
    }

    public async Task<ActionResult<List<ApplicationUser>>> GetStaffAsync(Guid businessId)
    {
        return await businessService.GetStaffAsync(businessId);
    }

    public async Task<ActionResult> AddAsync(Business business)
    {
        await businessService.AddAsync(business);
        return Created();
    }

    public async Task<ActionResult> UpdateAsync(Business business)
    {
        await businessService.UpdateAsync(business);
        return NoContent();
    }

    public async Task<ActionResult> DeleteAsync(Business business)
    {
        await businessService.DeleteAsync(business);
        return NoContent();
    }

    public async Task<ActionResult> AddStaffAsync(Guid businessId, string userId, string? userName = null)
    {
        var success = await businessService.AddStaffAsync(businessId, userId, userName);
        return success ? NoContent() : Conflict();
    }

    public async Task<ActionResult> RemoveStaffAsync(Guid businessId, string userId, string? userName = null)
    {
        var success = await businessService.RemoveStaffAsync(businessId, userId, userName);
        return success ? NoContent() : NotFound();
    }

    public async Task<ActionResult<Business>> ApplyAsync(Business business)
    {
        try
        {
            return await businessService.ApplyAsync(business);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    public async Task<ActionResult> ApproveAsync(Guid businessId)
    {
        await businessService.ApproveAsync(businessId);
        return NoContent();
    }

    public async Task<ActionResult> RejectAsync(Guid businessId, string reason)
    {
        await businessService.RejectAsync(businessId, reason);
        return NoContent();
    }

    public async Task<ActionResult> HideAsync(Guid businessId, string reason)
    {
        await businessService.HideAsync(businessId, reason);
        return NoContent();
    }

    public async Task<ActionResult> UnhideAsync(Guid businessId)
    {
        await businessService.UnhideAsync(businessId);
        return NoContent();
    }

    public async Task<ActionResult> SetHoursAsync(Guid businessId, List<BusinessHours> hours)
    {
        try
        {
            await businessService.SetHoursAsync(businessId, hours);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    public async Task<ActionResult<BusinessClosure>> AddClosureAsync(Guid businessId, DateOnly startDate, DateOnly endDate, string? reason = null)
    {
        try
        {
            return await businessService.AddClosureAsync(businessId, startDate, endDate, reason);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    public async Task<ActionResult> RemoveClosureAsync(Guid businessId, Guid closureId)
    {
        try
        {
            var success = await businessService.RemoveClosureAsync(businessId, closureId);
            return success ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }
}
