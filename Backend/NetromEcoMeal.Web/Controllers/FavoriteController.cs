using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class FavoriteController(IFavoriteService favoriteService) : ControllerBase
{
    public async Task<ActionResult<HashSet<Guid>>> GetMyFavoriteBusinessIdsAsync()
    {
        return await favoriteService.GetMyFavoriteBusinessIdsAsync();
    }

    public async Task<ActionResult<bool>> ToggleFavoriteAsync(Guid businessId)
    {
        try
        {
            return await favoriteService.ToggleFavoriteAsync(businessId);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Conflict(ex.Message);
        }
    }
}
