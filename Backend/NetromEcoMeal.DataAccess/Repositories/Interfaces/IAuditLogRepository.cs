using NetromEcoMeal.Entities;
using NetromEcoMeal.Models;

namespace NetromEcoMeal.Repositories.Interfaces;

public interface IAuditLogRepository
{
    public Task AddAsync(AuditLog entry);
    public Task<PaginatedList<AuditLog>> GetPagedAsync(int pageIndex, int pageSize, string? action, string? targetType, string? search);
}
