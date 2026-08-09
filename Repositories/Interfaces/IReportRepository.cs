using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

public interface IReportRepository
{
    public Task AddAsync(Report report);
    public Task<Report?> GetByIdAsync(Guid id);
    public Task<List<Report>> GetByStatusAsync(string status);
    public Task SaveChangesAsync();
}
