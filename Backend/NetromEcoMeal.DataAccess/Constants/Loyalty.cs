namespace NetromEcoMeal.Constants;

// Bounds enforced by BusinessService when a manager sets up a punch-card reward — see
// LoyaltyService for how the reward itself is evaluated at checkout.
public static class Loyalty
{
    // Below 2, "every order is a reward" isn't really a punch card.
    public const int MinPunchThreshold = 2;
    public const int MaxPunchThreshold = 50;
    public const decimal MinDiscountAmount = 0.5m;
}
