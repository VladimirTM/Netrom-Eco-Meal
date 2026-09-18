using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class BrandController(IBrandService brandService) : ControllerBase
{
    public async Task<ActionResult<List<Brand>>> GetAllAsync()
    {
        return await brandService.GetAllAsync();
    }

    public async Task<ActionResult<BrandDetailDto?>> GetPublicDetailAsync(Guid id)
    {
        var detail = await brandService.GetPublicDetailAsync(id);
        return detail is null ? NotFound() : detail;
    }

    public async Task<ActionResult> AddAsync(Brand brand)
    {
        try
        {
            await brandService.AddAsync(brand);
            return Created();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    public async Task<ActionResult> UpdateAsync(Brand brand)
    {
        try
        {
            await brandService.UpdateAsync(brand);
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
            await brandService.DeleteAsync(id);
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

    public async Task<ActionResult<HashSet<Guid>>> GetMyFavoriteBrandIdsAsync()
    {
        return await brandService.GetMyFavoriteBrandIdsAsync();
    }

    public async Task<ActionResult<bool>> ToggleFavoriteAsync(Guid brandId)
    {
        try
        {
            return await brandService.ToggleFavoriteAsync(brandId);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }
}
