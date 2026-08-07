using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class BusinessService(IBusinessRepository businessRepository, CurrentUserAccessor currentUser) : IBusinessService
{
    public async Task<List<Business>> GetAllAsync()
    {
        return await businessRepository.GetAllAsync();
    }

    public async Task<PaginatedList<Business>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessTypeId, string? staffUserId = null, string? sortBy = null, bool favoritesOnly = false, double? customerLat = null, double? customerLng = null)
    {
        string? favoritedByUserId = null;
        if (favoritesOnly)
        {
            var (_, userId) = await currentUser.GetCurrentUserAsync();
            // "" never matches a real UserId, so a signed-out user's Any() filter comes back empty.
            favoritedByUserId = userId ?? "";
        }

        return await businessRepository.GetPagedAsync(pageIndex, pageSize, search, businessTypeId, staffUserId, sortBy, favoritedByUserId, customerLat, customerLng);
    }

    public async Task<Business?> GetByIdAsync(Guid id)
    {
        return await businessRepository.GetByIdAsync(id);
    }

    public async Task<List<Business>> GetByStaffUserIdAsync(string userId)
    {
        return await businessRepository.GetByStaffUserIdAsync(userId);
    }

    public async Task<List<ApplicationUser>> GetStaffAsync(Guid businessId)
    {
        return await businessRepository.GetStaffAsync(businessId);
    }

    public async Task<bool> IsStaffAsync(Guid businessId, string userId)
    {
        return await businessRepository.IsStaffAsync(businessId, userId);
    }

    public async Task AddAsync(Business business)
    {
        var (isAdmin, _) = await currentUser.GetCurrentUserAsync();
        if (!isAdmin)
            throw new UnauthorizedAccessException("Only an admin can create businesses.");

        await businessRepository.AddAsync(business);
        await businessRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Business business)
    {
        var businessFromDb = await businessRepository.GetByIdAsync(business.Id);
        if (businessFromDb is null)
            return;

        var (isAdmin, userId) = await currentUser.GetCurrentUserAsync();
        if (!isAdmin && (userId is null || !await businessRepository.IsStaffAsync(businessFromDb.Id, userId)))
            throw new UnauthorizedAccessException("You can only edit your own business.");

        UpdateBusiness(business, businessFromDb);
        await businessRepository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Business business)
    {
        var (isAdmin, _) = await currentUser.GetCurrentUserAsync();
        if (!isAdmin)
            throw new UnauthorizedAccessException("Only an admin can delete businesses.");

        await businessRepository.DeleteAsync(business.Id);
        await businessRepository.SaveChangesAsync();
    }

    public async Task<bool> AddStaffAsync(Guid businessId, string userId)
    {
        var (isAdmin, _) = await currentUser.GetCurrentUserAsync();
        if (!isAdmin)
            throw new UnauthorizedAccessException("Only an admin can assign business staff.");

        try
        {
            return await businessRepository.AddStaffAsync(businessId, userId);
        }
        catch (DbUpdateException)
        {
            // A concurrent assignment already added this pair.
            return false;
        }
    }

    public async Task<bool> RemoveStaffAsync(Guid businessId, string userId)
    {
        var (isAdmin, _) = await currentUser.GetCurrentUserAsync();
        if (!isAdmin)
            throw new UnauthorizedAccessException("Only an admin can remove business staff.");

        return await businessRepository.RemoveStaffAsync(businessId, userId);
    }

    private static void UpdateBusiness(Business business, Business businessToUpdate)
    {
        businessToUpdate.Name = business.Name;
        businessToUpdate.Description = business.Description;
        businessToUpdate.Address = business.Address;
        businessToUpdate.ImageUrl = business.ImageUrl;
        businessToUpdate.Latitude = business.Latitude;
        businessToUpdate.Longitude = business.Longitude;
        businessToUpdate.BusinessTypeId = business.BusinessTypeId;
    }
}