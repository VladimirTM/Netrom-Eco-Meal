using NetromEcoMeal.Entities;

namespace NetromEcoMeal.DTOs;

public record BusinessHoursDto(DayOfWeek DayOfWeek, bool IsClosed, TimeOnly? OpenTime, TimeOnly? CloseTime);

public record BusinessClosureDto(Guid Id, DateOnly StartDate, DateOnly EndDate, string? Reason);

public record BusinessDto(
    Guid Id, string Name, string Description, string Address, string? ImageUrl,
    double? Latitude, double? Longitude, string Status, string? RejectionReason,
    bool IsHidden, string? HiddenReason, int? LoyaltyPunchThreshold, decimal? LoyaltyDiscountAmount,
    Guid BusinessTypeId, string BusinessTypeName, Guid? BrandId, string? BrandName,
    bool HasWebhookApiKey, DateTime? WebhookApiKeyLastUsedAt,
    List<BusinessHoursDto> Hours, List<BusinessClosureDto> Closures)
{
    public static BusinessDto FromEntity(Business b) => new(
        b.Id, b.Name, b.Description, b.Address, b.ImageUrl,
        b.Latitude, b.Longitude, b.Status, b.RejectionReason,
        b.IsHidden, b.HiddenReason, b.LoyaltyPunchThreshold, b.LoyaltyDiscountAmount,
        b.BusinessTypeId, b.BusinessType?.Name ?? "", b.BrandId, b.Brand?.Name,
        b.WebhookApiKeyHash is not null, b.WebhookApiKeyLastUsedAt,
        b.Hours.Select(h => new BusinessHoursDto(h.DayOfWeek, h.IsClosed, h.OpenTime, h.CloseTime)).ToList(),
        b.Closures.Select(c => new BusinessClosureDto(c.Id, c.StartDate, c.EndDate, c.Reason)).ToList());
}

// Shared by create/apply (POST) and update (PUT) — Status/approval/webhook-key fields are never
// settable from the request body; those go through their own endpoints.
public record BusinessWriteDto(
    string Name, string Description, string Address, string? ImageUrl,
    double? Latitude, double? Longitude, Guid BusinessTypeId, Guid? BrandId,
    int? LoyaltyPunchThreshold, decimal? LoyaltyDiscountAmount)
{
    public Business ToEntity(Guid id = default) => new()
    {
        Id = id,
        Name = Name,
        Description = Description,
        Address = Address,
        ImageUrl = ImageUrl,
        Latitude = Latitude,
        Longitude = Longitude,
        BusinessTypeId = BusinessTypeId,
        BrandId = BrandId,
        LoyaltyPunchThreshold = LoyaltyPunchThreshold,
        LoyaltyDiscountAmount = LoyaltyDiscountAmount,
    };
}

public record StaffMemberDto(string Id, string Name, string Email);

public record SetHoursRequestDto(List<BusinessHoursDto> Hours)
{
    public List<BusinessHours> ToEntities(Guid businessId) =>
        Hours.Select(h => new BusinessHours { BusinessId = businessId, DayOfWeek = h.DayOfWeek, IsClosed = h.IsClosed, OpenTime = h.OpenTime, CloseTime = h.CloseTime }).ToList();
}

public record AddClosureRequestDto(DateOnly StartDate, DateOnly EndDate, string? Reason);

public record AddStaffRequestDto(string UserId, string? UserName);

public record RejectBusinessRequestDto(string Reason);

public record HideRequestDto(string Reason);
