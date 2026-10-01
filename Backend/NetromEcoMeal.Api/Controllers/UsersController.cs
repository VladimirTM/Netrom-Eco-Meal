using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Models;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

// Admin-only; UserService itself enforces that via ICurrentUser.EnsureAdminAsync, same pattern
// every other service in this batch follows — [Authorize(Roles = Admin)] here is defense in depth.
[Route("api/users")]
[ApiController]
[Authorize(Roles = AppRoles.Admin)]
public class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedList<UserWithRole>>> GetPaged(
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? role = null) =>
        Ok(await userService.GetPagedAsync(pageIndex, pageSize, search, role));

    [HttpGet("by-role/{role}")]
    public async Task<ActionResult<List<UserWithRole>>> GetByRole(string role) =>
        Ok(await userService.GetByRoleAsync(role));

    // InvalidOperationException ("can't remove the last admin") is mapped to 409 by the global
    // exception middleware — see Program.cs — not caught here (3.1's single exception-to-status
    // mapping, replacing the per-action try/catch every façade controller used to need).
    [HttpPut("{userId}/role")]
    public async Task<IActionResult> UpdateRole(string userId, [FromQuery] string role)
    {
        var success = await userService.UpdateRoleAsync(userId, role);
        return success ? NoContent() : NotFound();
    }
}
