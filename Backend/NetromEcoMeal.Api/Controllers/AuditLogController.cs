using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Models;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/audit-log")]
[ApiController]
[Authorize]
public class AuditLogController(IAuditLogService auditLogService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedList<AuditLogDto>>> GetPaged(
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? action = null, [FromQuery] string? targetType = null, [FromQuery] string? search = null)
    {
        var page = await auditLogService.GetPagedAsync(pageIndex, pageSize, action, targetType, search);
        return Ok(page.MapItems(AuditLogDto.FromEntity));
    }
}
