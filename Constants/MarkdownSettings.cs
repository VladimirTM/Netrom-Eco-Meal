namespace Netrom_Eco_Meal.Constants;

// Governs the /packages markdown-pricing suggestion — IPackageService.GetMarkdownCandidatesAsync/GetMarkdownSuggestionAsync.
public static class MarkdownSettings
{
    // A live package with stock left becomes a markdown candidate once its pickup window has this long left.
    public static readonly TimeSpan ClosingWindow = TimeSpan.FromHours(3);

    // How far back to look for this business's own closed packages when building sell-through history.
    public const int LookbackDays = 90;

    // Caps how many historical records the tool hands the model.
    public const int MaxHistoryRecords = 25;

    // Never suggest cutting below this fraction of the current price — a floor against a hallucinated near-zero suggestion.
    public const decimal MinPriceFraction = 0.3m;
}
