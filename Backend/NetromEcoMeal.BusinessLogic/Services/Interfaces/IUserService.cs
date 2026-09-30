using NetromEcoMeal.Models;

namespace NetromEcoMeal.Services.Interfaces;

public record UserWithRole(string Id, string Name, string Email, string Role);

// Admin-only; enforced in the implementation via ICurrentUser.
public interface IUserService
{
    public Task<List<UserWithRole>> GetAllAsync();
    public Task<List<UserWithRole>> GetByRoleAsync(string role);
    public Task<PaginatedList<UserWithRole>> GetPagedAsync(int pageIndex, int pageSize, string? search, string? role);
    public Task<bool> UpdateRoleAsync(string userId, string role);
}