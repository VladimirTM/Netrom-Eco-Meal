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

    public async Task<ActionResult<PaginatedList<Business>>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessTypeId)
    {
        return await businessService.GetPagedAsync(pageIndex, pageSize, search, businessTypeId);
    }

    public async Task<ActionResult<Business?>> GetByIdAsync(Guid id)
    {
        return await businessService.GetByIdAsync(id);
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

    public async Task<ActionResult> AssignManagerAsync(Guid businessId, string? managerId)
    {
        var success = await businessService.AssignManagerAsync(businessId, managerId);
        return success ? NoContent() : Conflict();
    }
}