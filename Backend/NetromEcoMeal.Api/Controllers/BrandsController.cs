using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/brands")]
[ApiController]
public class BrandsController(IBrandService brandService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BrandDto>>> GetAll() =>
        Ok((await brandService.GetAllAsync()).Select(BrandDto.FromEntity).ToList());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BrandDetailResponseDto>> GetDetail(Guid id)
    {
        var detail = await brandService.GetPublicDetailAsync(id);
        if (detail is null)
            return NotFound();

        return Ok(new BrandDetailResponseDto(
            BrandDto.FromEntity(detail.Brand),
            detail.Locations.Select(BusinessDto.FromEntity).ToList(),
            detail.AverageRating,
            detail.RatingCount));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Add([FromBody] BrandWriteDto request)
    {
        var entity = request.ToEntity(Guid.NewGuid());
        await brandService.AddAsync(entity);
        return CreatedAtAction(nameof(GetDetail), new { id = entity.Id }, BrandDto.FromEntity(entity));
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] BrandWriteDto request)
    {
        await brandService.UpdateAsync(request.ToEntity(id));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        await brandService.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("favorites/mine")]
    [Authorize]
    public async Task<ActionResult<HashSet<Guid>>> GetMyFavoriteIds() =>
        Ok(await brandService.GetMyFavoriteBrandIdsAsync());

    [HttpPost("{id:guid}/favorite")]
    [Authorize]
    public async Task<ActionResult<bool>> ToggleFavorite(Guid id) =>
        Ok(await brandService.ToggleFavoriteAsync(id));
}
