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
    public async Task<ActionResult<List<Business>>> GetAllAsync()
    {
        return await businessService.GetAllAsync();
    }

    public async Task<ActionResult<PaginatedList<Business>>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessTypeId, string? staffUserId = null, string? sortBy = null, bool favoritesOnly = false, double? customerLat = null, double? customerLng = null)
    {
        return await businessService.GetPagedAsync(pageIndex, pageSize, search, businessTypeId, staffUserId, sortBy, favoritesOnly, customerLat, customerLng);
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

    public async Task<ActionResult> AddStaffAsync(Guid businessId, string userId)
    {
        var success = await businessService.AddStaffAsync(businessId, userId);
        return success ? NoContent() : Conflict();
    }

    public async Task<ActionResult> RemoveStaffAsync(Guid businessId, string userId)
    {
        var success = await businessService.RemoveStaffAsync(businessId, userId);
        return success ? NoContent() : NotFound();
    }
}