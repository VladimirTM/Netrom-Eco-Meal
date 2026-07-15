using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

// Admin-only; enforced here via CurrentUserAccessor since callers invoke this in-process
// (through an injected controller), not through an [Authorize]-guarded HTTP endpoint.
public class UserService(
    UserManager<ApplicationUser> userManager,
    EcoMealDbContext dbContext,
    IBusinessService businessService,
    CurrentUserAccessor currentUser) : IUserService
{
    public async Task<List<UserWithRole>> GetAllAsync()
    {
        await EnsureAdminAsync();

        var users = await userManager.Users.OrderBy(u => u.Name).ToListAsync();

        var roleByUserId = (await (
                from userRole in dbContext.UserRoles
                join role in dbContext.Roles on userRole.RoleId equals role.Id
                select new { userRole.UserId, role.Name }
            ).ToListAsync())
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First().Name ?? AppRoles.Customer);

        return users
            .Select(u => new UserWithRole(u.Id, u.Name, u.Email!, roleByUserId.GetValueOrDefault(u.Id, AppRoles.Customer)))
            .ToList();
    }

    public async Task<List<UserWithRole>> GetByRoleAsync(string role)
    {
        await EnsureAdminAsync();

        var users = await userManager.GetUsersInRoleAsync(role);

        return users
            .OrderBy(u => u.Name)
            .Select(u => new UserWithRole(u.Id, u.Name, u.Email!, role))
            .ToList();
    }

    public async Task<PaginatedList<UserWithRole>> GetPagedAsync(int pageIndex, int pageSize, string? search, string? role)
    {
        await EnsureAdminAsync();

        // Left join so a roleless user still shows up (defaults to Customer, matching
        // GetAllAsync/GetByRoleAsync). Projects to an anonymous type, not UserWithRole —
        // EF can't translate OrderBy once a query is projected through a record constructor.
        var query =
            from u in userManager.Users
            join ur in dbContext.UserRoles on u.Id equals ur.UserId into urJoin
            from ur in urJoin.DefaultIfEmpty()
            join r in dbContext.Roles on ur.RoleId equals r.Id into rJoin
            from r in rJoin.DefaultIfEmpty()
            select new { u.Id, u.Name, u.Email, Role = r.Name ?? AppRoles.Customer };

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => EF.Functions.ILike(u.Name, $"%{search}%") || EF.Functions.ILike(u.Email!, $"%{search}%"));

        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.Role == role);

        return await PaginatedList<UserWithRole>.CreateAsync(
            query.OrderBy(u => u.Name),
            u => new UserWithRole(u.Id, u.Name, u.Email!, u.Role),
            pageIndex, pageSize);
    }

    public async Task<bool> UpdateRoleAsync(string userId, string role)
    {
        await EnsureAdminAsync();

        if (!AppRoles.AllRoles.Contains(role))
            return false;

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return false;

        var currentRoles = await userManager.GetRolesAsync(user);

        // Never leave the platform with zero admins.
        if (currentRoles.Contains(AppRoles.Admin) && role != AppRoles.Admin)
        {
            var adminCount = (await userManager.GetUsersInRoleAsync(AppRoles.Admin)).Count;
            if (adminCount <= 1)
                throw new InvalidOperationException("Cannot remove the last remaining admin.");
        }

        await userManager.RemoveFromRolesAsync(user, currentRoles);
        await userManager.AddToRoleAsync(user, role);

        // Moving away from BusinessManager releases whatever business they managed.
        if (role != AppRoles.BusinessManager)
        {
            var managedBusiness = await businessService.GetByManagerIdAsync(userId);
            if (managedBusiness is not null)
                await businessService.AssignManagerAsync(managedBusiness.Id, null);
        }

        return true;
    }

    private async Task EnsureAdminAsync()
    {
        var (isAdmin, _) = await currentUser.GetCurrentUserAsync();
        if (!isAdmin)
            throw new UnauthorizedAccessException("Only an admin can manage users.");
    }
}
