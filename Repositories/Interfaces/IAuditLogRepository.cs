using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

public interface IAuditLogRepository
{
    public Task AddAsync(AuditLog entry);
    public Task<PaginatedList<AuditLog>> GetPagedAsync(int pageIndex, int pageSize, string? action, string? targetType, string? search);
}
