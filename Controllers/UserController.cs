using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class UserController(IUserService userService) : ControllerBase
{
    public async Task<ActionResult<List<UserWithRole>>> GetAllAsync()
    {
        return await userService.GetAllAsync();
    }

    public async Task<ActionResult<List<UserWithRole>>> GetByRoleAsync(string role)
    {
        return await userService.GetByRoleAsync(role);
    }

    public async Task<ActionResult<PaginatedList<UserWithRole>>> GetPagedAsync(int pageIndex, int pageSize, string? search, string? role)
    {
        return await userService.GetPagedAsync(pageIndex, pageSize, search, role);
    }

    public async Task<ActionResult> UpdateRoleAsync(string userId, string role)
    {
        try
        {
            var success = await userService.UpdateRoleAsync(userId, role);
            return success ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }
}