namespace Netrom_Eco_Meal.Constants;

// Stripe's own minimum chargeable amount for the RON checkout currency — distinct from
// Loyalty.MinDiscountAmount, a manager's reward-config floor, not a Stripe limit.
public static class Checkout
{
    public const decimal MinChargeableAmount = 2.0m;
}
