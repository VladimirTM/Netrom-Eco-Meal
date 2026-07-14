namespace Netrom_Eco_Meal.Constants;

// Small formatting/filtering helpers shared by list and avatar UI.
public static class TextHelpers
{
    public static string GetInitial(string? name) =>
        string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();

    public static bool MatchesSearch(string search, params string?[] fields) =>
        string.IsNullOrEmpty(search) ||
        fields.Any(f => f is not null && f.Contains(search, StringComparison.OrdinalIgnoreCase));
}
