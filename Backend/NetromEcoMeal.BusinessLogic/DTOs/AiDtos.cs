using NetromEcoMeal.Models;

namespace NetromEcoMeal.DTOs;

public record ParseSearchIntentRequestDto(string Utterance, SearchIntent? PreviousIntent);

public record ProposeBasketRequestDto(int PeopleCount, decimal Budget, string? DietaryTag);

public record BasketPlanItemDto(PackageDto Package, int Quantity, string Reason, decimal LineTotal);

public record BasketPlanDto(List<BasketPlanItemDto> Items, decimal TotalPrice, string Explanation)
{
    public static BasketPlanDto FromModel(BasketPlan plan) => new(
        plan.Items.Select(i => new BasketPlanItemDto(PackageDto.FromEntity(i.Package), i.Quantity, i.Reason, i.LineTotal)).ToList(),
        plan.TotalPrice, plan.Explanation);
}

public record DraftDescriptionRequestDto(string Name, string PackageTypeName, List<string> DietaryTags);
