namespace Netrom_Eco_Meal.Services.Interfaces;

public record UserWithRole(string Id, string Name, string Email, string Role);

public interface IUserService
{
    public Task<List<UserWithRole>> GetAllAsync();
    public Task<List<UserWithRole>> GetByRoleAsync(string role);
    public Task<bool> UpdateRoleAsync(string userId, string role);
}