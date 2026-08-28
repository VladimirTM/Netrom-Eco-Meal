using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class BasketPlannerController(IBasketPlannerAgent basketPlannerAgent) : ControllerBase
{
    public async Task<ActionResult<BasketPlan>> ProposeBasketAsync(int peopleCount, decimal budget, string? dietaryTag)
    {
        try
        {
            return await basketPlannerAgent.ProposeBasketAsync(peopleCount, budget, dietaryTag);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }
}
