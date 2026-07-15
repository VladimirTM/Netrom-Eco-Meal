using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class ReviewController(IReviewService reviewService) : ControllerBase
{
    public async Task<ActionResult<List<Review>>> GetAllAsync()
    {
        return await reviewService.GetAllAsync();
    }

    public async Task<ActionResult<List<Review>>> GetByBusinessAsync(Guid businessId)
    {
        return await reviewService.GetByBusinessIdAsync(businessId);
    }

    public async Task<ActionResult<List<Review>>> GetByBusinessesAsync(List<Guid> businessIds)
    {
        return await reviewService.GetByBusinessIdsAsync(businessIds);
    }

    public async Task<ActionResult<ReviewContext>> GetContextAsync(Guid businessId)
    {
        return await reviewService.GetContextAsync(businessId);
    }

    public async Task<ActionResult> SubmitAsync(Guid businessId, int rating, string? comment)
    {
        try
        {
            await reviewService.SubmitAsync(businessId, rating, comment);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Conflict(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }
}
