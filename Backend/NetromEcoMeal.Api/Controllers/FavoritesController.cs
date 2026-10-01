using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/favorites")]
[ApiController]
[Authorize]
public class FavoritesController(IFavoriteService favoriteService) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<HashSet<Guid>>> GetMyFavoriteBusinessIds() =>
        Ok(await favoriteService.GetMyFavoriteBusinessIdsAsync());

    [HttpPost("{businessId:guid}/toggle")]
    public async Task<ActionResult<bool>> ToggleFavorite(Guid businessId) =>
        Ok(await favoriteService.ToggleFavoriteAsync(businessId));
}
