using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class AuditLogController(IAuditLogService auditLogService) : ControllerBase
{
    public async Task<ActionResult<PaginatedList<AuditLog>>> GetPagedAsync(int pageIndex, int pageSize, string? action, string? targetType, string? search)
    {
        try
        {
            return await auditLogService.GetPagedAsync(pageIndex, pageSize, action, targetType, search);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }
}
