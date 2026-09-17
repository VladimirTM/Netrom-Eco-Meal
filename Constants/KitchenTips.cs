namespace Netrom_Eco_Meal.Constants;

// Bounds on KitchenTipService.SubmitAsync — a tip is a short practical hint ("use the side door
// after 8pm"), not a review, so it gets a much tighter cap than Review.Comment.
public static class KitchenTips
{
    public const int MaxLength = 200;
}
