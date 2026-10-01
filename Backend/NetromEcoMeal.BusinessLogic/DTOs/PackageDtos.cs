using NetromEcoMeal.Entities;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.DTOs;

public record PackageDto(
    Guid Id, Guid BusinessId, string BusinessName, Guid PackageTypeId, string PackageTypeName,
    string Name, string Description, decimal Price, int Quantity, decimal WeightKg,
    List<string> DietaryTags, DateTime PickupStart, DateTime PickupEnd, string? ImageUrl,
    Guid? TemplateId, bool IsHidden, string? HiddenReason, DateTime? MarkdownDismissedAt,
    DateTime? DonatedAt, DateTime? DonationOfferedAt)
{
    public static PackageDto FromEntity(Package p) => new(
        p.Id, p.BusinessId, p.Business?.Name ?? "", p.PackageTypeId, p.PackageType?.Name ?? "",
        p.Name, p.Description, p.Price, p.Quantity, p.WeightKg,
        p.DietaryTags, p.PickupStart, p.PickupEnd, p.ImageUrl,
        p.TemplateId, p.IsHidden, p.HiddenReason, p.MarkdownDismissedAt,
        p.DonatedAt, p.DonationOfferedAt);
}

// Status/moderation/donation fields are never settable from the request body, same as BusinessWriteDto.
public record PackageWriteDto(
    Guid BusinessId, Guid PackageTypeId, string Name, string Description, decimal Price,
    int Quantity, decimal WeightKg, List<string> DietaryTags, DateTime PickupStart,
    DateTime PickupEnd, string? ImageUrl)
{
    public Package ToEntity(Guid id = default) => new()
    {
        Id = id,
        BusinessId = BusinessId,
        PackageTypeId = PackageTypeId,
        Name = Name,
        Description = Description,
        Price = Price,
        Quantity = Quantity,
        WeightKg = WeightKg,
        DietaryTags = DietaryTags,
        PickupStart = PickupStart,
        PickupEnd = PickupEnd,
        ImageUrl = ImageUrl,
    };
}

public record AdjustQuantityManyRequestDto(List<Guid> PackageIds, int Delta);

public record ExtendPickupWindowManyRequestDto(List<Guid> PackageIds, double Hours);

public record MarkdownSuggestionDto(decimal CurrentPrice, decimal SuggestedPrice, string Explanation);

public record PackageTemplateDto(
    Guid Id, Guid BusinessId, Guid PackageTypeId, string PackageTypeName, string Name, string Description,
    decimal Price, int Quantity, decimal WeightKg, List<string> DietaryTags,
    TimeSpan PickupStartTimeUtc, TimeSpan PickupEndTimeUtc, string? ImageUrl, bool IsActive, DateOnly? LastGeneratedDate)
{
    public static PackageTemplateDto FromEntity(PackageTemplate t) => new(
        t.Id, t.BusinessId, t.PackageTypeId, t.PackageType?.Name ?? "", t.Name, t.Description,
        t.Price, t.Quantity, t.WeightKg, t.DietaryTags,
        t.PickupStartTimeUtc, t.PickupEndTimeUtc, t.ImageUrl, t.IsActive, t.LastGeneratedDate);
}

public record CreateTemplateRequestDto(Guid PackageId, TimeSpan PickupStartTimeUtc, TimeSpan PickupEndTimeUtc);

public record SetTemplateActiveRequestDto(bool IsActive);

public record ReviewDto(Guid Id, Guid BusinessId, string UserId, string UserName, int Rating, string? Comment, DateTime CreatedAt, Guid? PackageId)
{
    public static ReviewDto FromEntity(Review r) => new(r.Id, r.BusinessId, r.UserId, r.User?.Name ?? "", r.Rating, r.Comment, r.CreatedAt, r.PackageId);
}

public record ReviewContextDto(bool CanReview, ReviewDto? MyReview, List<PackageDto> ReviewablePackages)
{
    public static ReviewContextDto FromModel(ReviewContext ctx) => new(
        ctx.CanReview, ctx.MyReview is null ? null : ReviewDto.FromEntity(ctx.MyReview),
        ctx.ReviewablePackages.Select(PackageDto.FromEntity).ToList());
}

public record SubmitReviewRequestDto(int Rating, string? Comment, Guid? PackageId);

public record KitchenTipDto(Guid Id, Guid BusinessId, string UserId, string UserName, string Tip, DateTime CreatedAt)
{
    public static KitchenTipDto FromEntity(KitchenTip t) => new(t.Id, t.BusinessId, t.UserId, t.User?.Name ?? "", t.Tip, t.CreatedAt);
}

public record SubmitKitchenTipRequestDto(string Tip);
