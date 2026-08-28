namespace Netrom_Eco_Meal.Services.Interfaces;

// Thin wrapper over IChatClient — drafts the nudge copy for NearExpiryNudgeService, same shape as
// IPackageAiAssistant/ISearchIntentParser.
public interface INearExpiryNudgeComposer
{
    Task<string> ComposeAsync(string packageName, string businessName, int quantity, TimeSpan timeUntilClose, string? matchedDietaryTag, CancellationToken cancellationToken = default);
}
