namespace Netrom_Eco_Meal.Services.Interfaces;

// Periodic sweep flagging packages closing soon with stock still unclaimed — see
// NearExpiryNudgeSweepService for the BackgroundService that calls this.
public interface INearExpiryNudgeService
{
    // Returns how many nudge notifications were sent this sweep.
    Task<int> SweepAsync();
}
