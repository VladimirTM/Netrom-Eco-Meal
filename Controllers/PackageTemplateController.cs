using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class PackageTemplateController(IPackageTemplateService templateService) : ControllerBase
{
    public async Task<ActionResult<List<PackageTemplate>>> GetAllAsync()
    {
        return await templateService.GetAllAsync();
    }

    public async Task<ActionResult<List<PackageTemplate>>> GetByBusinessIdAsync(Guid businessId)
    {
        return await templateService.GetByBusinessIdAsync(businessId);
    }

    public async Task<ActionResult<PackageTemplate>> CreateFromPackageAsync(Guid packageId, TimeSpan pickupStartTimeUtc, TimeSpan pickupEndTimeUtc)
    {
        return await templateService.CreateFromPackageAsync(packageId, pickupStartTimeUtc, pickupEndTimeUtc);
    }

    public async Task<ActionResult> SetActiveAsync(Guid id, bool isActive)
    {
        await templateService.SetActiveAsync(id, isActive);
        return NoContent();
    }

    public async Task<ActionResult> DeleteAsync(Guid id)
    {
        await templateService.DeleteAsync(id);
        return NoContent();
    }
}
