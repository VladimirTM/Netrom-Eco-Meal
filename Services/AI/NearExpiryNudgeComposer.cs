using Microsoft.Extensions.AI;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services.AI;

// Same null-chatClient-degrades-gracefully constructor shape as PackageAiAssistant/SearchIntentParser.
// Pure text generation, no tool-calling and no JSON schema needed here.
public class NearExpiryNudgeComposer(IChatClient? chatClient = null) : INearExpiryNudgeComposer
{
    public async Task<string> ComposeAsync(string packageName, string businessName, int quantity, TimeSpan timeUntilClose, string? matchedDietaryTag, CancellationToken cancellationToken = default)
    {
        if (chatClient is null)
            throw new InvalidOperationException("AI features aren't available yet — set Ollama:BaseUrl to enable them.");

        var minutes = Math.Max(1, (int)Math.Round(timeUntilClose.TotalMinutes));
        var matchLine = matchedDietaryTag is null
            ? ""
            : $"""

                The customer has completed a {matchedDietaryTag} order from this kitchen before —
                briefly note that this package matches what they usually order.
                """;

        var prompt = $"""
            Write a short, urgent-but-friendly push notification (one sentence, no emoji, no
            quotation marks, under 160 characters) telling a customer that a surplus food package
            is closing soon with stock still unclaimed, nudging them to grab it before it's gone.

            Package: {packageName}
            Kitchen: {businessName}
            Portions left: {quantity}
            Closes in: {minutes} minutes
            {matchLine}
            Only output the notification text, nothing else.
            """;

        try
        {
            var response = await chatClient.GetResponseAsync(prompt, cancellationToken: cancellationToken);
            return response.Text.Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("The AI assistant couldn't draft a nudge right now — it'll retry on the next sweep.", ex);
        }
    }
}
