using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class BusinessTypeController(IBusinessTypeService businessTypeService) : ControllerBase
{
    public async Task<ActionResult<List<BusinessType>>> GetAllAsync()
    {
        return await businessTypeService.GetAllAsync();
    }

    public async Task<ActionResult> AddAsync(BusinessType businessType)
    {
        try
        {
            await businessTypeService.AddAsync(businessType);
            return Created();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    public async Task<ActionResult> UpdateAsync(BusinessType businessType)
    {
        try
        {
            await businessTypeService.UpdateAsync(businessType);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    public async Task<ActionResult> DeleteAsync(Guid id)
    {
        try
        {
            await businessTypeService.DeleteAsync(id);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }
}
