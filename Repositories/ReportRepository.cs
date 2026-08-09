using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

public class ReportRepository(EcoMealDbContext context) : IReportRepository
{
    public async Task AddAsync(Report report)
    {
        await context.Reports.AddAsync(report);
        await context.SaveChangesAsync();
    }

    public async Task<Report?> GetByIdAsync(Guid id)
    {
        return await context.Reports.Include(r => r.Reporter).FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<List<Report>> GetByStatusAsync(string status)
    {
        return await context.Reports
            .Include(r => r.Reporter)
            .Where(r => r.Status == status)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}
