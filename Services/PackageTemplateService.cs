using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

// Write methods are restricted to admins and the template's own business manager.
public class PackageTemplateService(
    IPackageTemplateRepository templateRepository,
    IPackageRepository packageRepository,
    IBusinessService businessService,
    CurrentUserAccessor currentUser) : IPackageTemplateService
{
    public async Task<List<PackageTemplate>> GetAllAsync()
    {
        return await templateRepository.GetAllAsync();
    }

    public async Task<List<PackageTemplate>> GetByBusinessIdAsync(Guid businessId)
    {
        return await templateRepository.GetByBusinessIdAsync(businessId);
    }

    // Copies the just-created package's fields into a new template and links the two — it
    // becomes the template's first instance.
    public async Task<PackageTemplate> CreateFromPackageAsync(Guid packageId, TimeSpan pickupStartTimeUtc, TimeSpan pickupEndTimeUtc)
    {
        var package = await packageRepository.GetByIdAsync(packageId)
            ?? throw new InvalidOperationException("This package no longer exists.");

        await EnsureCanManageBusinessAsync(package.BusinessId);

        var template = new PackageTemplate
        {
            Id = Guid.NewGuid(),
            BusinessId = package.BusinessId,
            PackageTypeId = package.PackageTypeId,
            Name = package.Name,
            Description = package.Description,
            Price = package.Price,
            Quantity = package.Quantity,
            WeightKg = package.WeightKg,
            DietaryTags = [..package.DietaryTags],
            PickupStartTimeUtc = pickupStartTimeUtc,
            PickupEndTimeUtc = pickupEndTimeUtc,
            ImageUrl = package.ImageUrl,
            LastGeneratedDate = DateOnly.FromDateTime(package.PickupStart),
        };

        await templateRepository.AddAsync(template);
        package.TemplateId = template.Id;
        await templateRepository.SaveChangesAsync();

        return template;
    }

    public async Task SetActiveAsync(Guid id, bool isActive)
    {
        var template = await templateRepository.GetByIdAsync(id);
        if (template is null)
            return;

        await EnsureCanManageBusinessAsync(template.BusinessId);

        template.IsActive = isActive;
        await templateRepository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var template = await templateRepository.GetByIdAsync(id);
        if (template is null)
            return;

        await EnsureCanManageBusinessAsync(template.BusinessId);

        await templateRepository.DeleteAsync(id);
        await templateRepository.SaveChangesAsync();
    }

    public async Task<int> GenerateDueInstancesAsync()
    {
        var templates = await templateRepository.GetActiveAsync();
        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        var generated = 0;
        var touched = false;

        foreach (var template in templates)
        {
            if (template.LastGeneratedDate == todayUtc)
                continue;

            var pickupStart = CombineUtc(todayUtc, template.PickupStartTimeUtc);
            // EndTime <= StartTime means the window crosses midnight — end falls the next day.
            var pickupEnd = template.PickupEndTimeUtc <= template.PickupStartTimeUtc
                ? CombineUtc(todayUtc.AddDays(1), template.PickupEndTimeUtc)
                : CombineUtc(todayUtc, template.PickupEndTimeUtc);

            touched = true;

            // The sweep missed a day (app downtime, etc.) and today's window already closed —
            // skip straight to tomorrow rather than generating a package that's dead on arrival.
            if (pickupEnd <= DateTime.UtcNow)
            {
                template.LastGeneratedDate = todayUtc;
                continue;
            }

            var package = new Package
            {
                BusinessId = template.BusinessId,
                PackageTypeId = template.PackageTypeId,
                Name = template.Name,
                Description = template.Description,
                Price = template.Price,
                Quantity = template.Quantity,
                WeightKg = template.WeightKg,
                DietaryTags = [..template.DietaryTags],
                PickupStart = pickupStart,
                PickupEnd = pickupEnd,
                ImageUrl = template.ImageUrl,
                TemplateId = template.Id,
            };

            await packageRepository.AddAsync(package);
            template.LastGeneratedDate = todayUtc;
            generated++;
        }

        if (touched)
            await templateRepository.SaveChangesAsync();

        return generated;
    }

    private static DateTime CombineUtc(DateOnly date, TimeSpan timeOfDay) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue) + timeOfDay, DateTimeKind.Utc);

    private async Task EnsureCanManageBusinessAsync(Guid businessId)
    {
        var (isAdmin, userId) = await currentUser.GetCurrentUserAsync();
        if (isAdmin)
            return;

        if (userId is null || !await businessService.IsStaffAsync(businessId, userId))
            throw new UnauthorizedAccessException("You can only manage templates that belong to your business.");
    }
}
