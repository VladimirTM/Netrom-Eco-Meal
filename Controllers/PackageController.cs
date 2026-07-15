using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class PackageController(IPackageService packageService) : ControllerBase
{
    public async Task<ActionResult<List<Package>>> GetAllAsync()
    {
        return await packageService.GetAllAsync();
    }

    public async Task<ActionResult<PaginatedList<Package>>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessId, Guid? packageTypeId)
    {
        return await packageService.GetPagedAsync(pageIndex, pageSize, search, businessId, packageTypeId);
    }

    public async Task<ActionResult<Package?>> GetByIdAsync(Guid id)
    {
        return await packageService.GetByIdAsync(id);
    }

    public async Task<ActionResult> AddAsync(Package package)
    {
        await packageService.AddAsync(package);
        return Created();
    }

    public async Task<ActionResult> UpdateAsync(Package package)
    {
        await packageService.UpdateAsync(package);
        return NoContent();
    }
    
    public async Task<ActionResult> DeleteAsync(Package package)
    {
        await packageService.DeleteAsync(package);
        return NoContent();
    }
}