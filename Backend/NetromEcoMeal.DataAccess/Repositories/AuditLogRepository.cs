using Microsoft.EntityFrameworkCore;
using NetromEcoMeal.Database;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Models;
using NetromEcoMeal.Repositories.Interfaces;

namespace NetromEcoMeal.Repositories;

public class AuditLogRepository(EcoMealDbContext context) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog entry)
    {
        await context.AuditLogs.AddAsync(entry);
        await context.SaveChangesAsync();
    }

    public async Task<PaginatedList<AuditLog>> GetPagedAsync(int pageIndex, int pageSize, string? action, string? targetType, string? search)
    {
        var query = context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        if (!string.IsNullOrWhiteSpace(targetType))
            query = query.Where(a => a.TargetType == targetType);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a =>
                EF.Functions.ILike(a.ActorName, $"%{search}%") ||
                EF.Functions.ILike(a.TargetName, $"%{search}%"));

        return await PaginatedList<AuditLog>.CreateAsync(query.OrderByDescending(a => a.CreatedAt), pageIndex, pageSize);
    }
}
