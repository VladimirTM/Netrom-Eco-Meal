using Microsoft.Extensions.AI;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services.AI;

// chatClient is only registered in Program.cs when Ollama:BaseUrl is configured — the default
// constructor parameter below lets DI resolve this service to a null client instead of throwing
// when it isn't, same "empty/unreachable -> friendly error" convention as StripeGateway.
public class PackageAiAssistant(IChatClient? chatClient = null) : IPackageAiAssistant
{
    public async Task<string> DraftDescriptionAsync(string name, string packageTypeName, IReadOnlyList<string> dietaryTags, CancellationToken cancellationToken = default)
    {
        if (chatClient is null)
            throw new InvalidOperationException("AI features aren't available yet — set Ollama:BaseUrl to enable them.");

        var tags = dietaryTags.Count > 0 ? string.Join(", ", dietaryTags) : "none";
        var prompt = $"""
            Write a short, appetizing customer-facing description (1-2 sentences, no marketing
            fluff, no emoji, no quotation marks) for a surplus food package sold at a discount to
            reduce food waste.
            Name: {name}
            Type: {packageTypeName}
            Dietary/allergen tags: {tags}
            Only output the description text, nothing else.
            """;

        try
        {
            var response = await chatClient.GetResponseAsync(prompt, cancellationToken: cancellationToken);
            return response.Text.Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("The AI assistant couldn't generate a description right now — try again in a moment.", ex);
        }
    }
}
