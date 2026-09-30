using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Models;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Controllers;

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
