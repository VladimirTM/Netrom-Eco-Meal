using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class StandingOrderController(IStandingOrderService standingOrderService) : ControllerBase
{
    public async Task<ActionResult<List<StandingOrder>>> GetMyStandingOrdersAsync()
    {
        return await standingOrderService.GetMyStandingOrdersAsync();
    }

    public async Task<ActionResult<StandingOrder>> CreateAsync(Guid businessId, Guid? packageTypeId, string? dietaryTag, decimal maxWeeklySpend)
    {
        try
        {
            return await standingOrderService.CreateAsync(businessId, packageTypeId, dietaryTag, maxWeeklySpend);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            return Conflict(ex.Message);
        }
    }

    public async Task<ActionResult> UpdateAsync(Guid id, decimal maxWeeklySpend, bool isActive)
    {
        try
        {
            await standingOrderService.UpdateAsync(id, maxWeeklySpend, isActive);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Conflict(ex.Message);
        }
    }

    public async Task<ActionResult> DeleteAsync(Guid id)
    {
        try
        {
            await standingOrderService.DeleteAsync(id);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Conflict(ex.Message);
        }
    }
}
