using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class PackageTypeController(IPackageTypeService packageTypeService) : ControllerBase
{
    public async Task<ActionResult<List<PackageType>>> GetAllAsync()
    {
        return await packageTypeService.GetAllAsync();
    }

    public async Task<ActionResult> AddAsync(PackageType packageType)
    {
        try
        {
            await packageTypeService.AddAsync(packageType);
            return Created();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    public async Task<ActionResult> UpdateAsync(PackageType packageType)
    {
        try
        {
            await packageTypeService.UpdateAsync(packageType);
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
            await packageTypeService.DeleteAsync(id);
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
