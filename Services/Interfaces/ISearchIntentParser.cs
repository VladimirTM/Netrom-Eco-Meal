using Netrom_Eco_Meal.Models;

namespace Netrom_Eco_Meal.Services.Interfaces;

// Turns a shopper's free-text query into a SearchIntent Home.razor applies against
// BusinessController.GetPagedAsync's existing filters.
public interface ISearchIntentParser
{
    // previousIntent, when given, lets a follow-up utterance ("cheaper", "gluten-free only")
    // refine the prior turn instead of starting over — see SearchIntentParser for why that's
    // passed as explicit JSON context rather than replayed chat history.
    Task<SearchIntent> ParseAsync(string utterance, SearchIntent? previousIntent = null, CancellationToken cancellationToken = default);
}
