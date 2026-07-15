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

    public async Task<PaginatedList<Business>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessTypeId)
    {
        return await businessRepository.GetPagedAsync(pageIndex, pageSize, search, businessTypeId);
    }

    public async Task<Business?> GetByIdAsync(Guid id)
    {
        return await businessRepository.GetByIdAsync(id);
    }

    public async Task<Business?> GetByManagerIdAsync(string managerId)
    {
        return await businessRepository.GetByManagerIdAsync(managerId);
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
        if (!isAdmin && businessFromDb.ManagerId != userId)
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

    public async Task<bool> AssignManagerAsync(Guid businessId, string? managerId)
    {
        var (isAdmin, _) = await currentUser.GetCurrentUserAsync();
        if (!isAdmin)
            throw new UnauthorizedAccessException("Only an admin can assign business managers.");

        var business = await businessRepository.GetByIdAsync(businessId);
        if (business is null)
            return false;

        if (managerId is not null)
        {
            var previouslyManaged = await businessRepository.GetByManagerIdAsync(managerId);
            if (previouslyManaged is not null && previouslyManaged.Id != businessId)
                previouslyManaged.ManagerId = null;
        }

        business.ManagerId = managerId;

        try
        {
            await businessRepository.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A concurrent assignment already gave this manager a different business.
            return false;
        }

        return true;
    }

    private static void UpdateBusiness(Business business, Business businessToUpdate)
    {
        businessToUpdate.Name = business.Name;
        businessToUpdate.Description = business.Description;
        businessToUpdate.Address = business.Address;
        businessToUpdate.ImageUrl = business.ImageUrl;
        businessToUpdate.BusinessTypeId = business.BusinessTypeId;
    }
}