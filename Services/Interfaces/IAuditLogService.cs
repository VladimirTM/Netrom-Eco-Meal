using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;

namespace Netrom_Eco_Meal.Services.Interfaces;

// Write side (LogAsync) is called from within other services after a mutation already succeeded
// — never exposed over a controller, so an entry always reflects something that really happened.
// Read side (GetPagedAsync) is admin-only.
public interface IAuditLogService
{
    public Task LogAsync(string action, string targetType, string? targetId, string targetName, string? details = null);
    public Task<PaginatedList<AuditLog>> GetPagedAsync(int pageIndex, int pageSize, string? action, string? targetType, string? search);
}
