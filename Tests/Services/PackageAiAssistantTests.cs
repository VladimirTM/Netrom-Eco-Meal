using Microsoft.Extensions.AI;
using Moq;
using Netrom_Eco_Meal.Services.AI;

namespace Netrom_Eco_Meal.Tests.Services;

// IChatClient itself is never exercised against a real Ollama instance here — just the "no
// client configured -> friendly error" degradation and the happy-path prompt/response plumbing.
public class PackageAiAssistantTests
{
    [Fact]
    public async Task DraftDescriptionAsync_NoChatClientConfigured_ThrowsFriendlyError()
    {
        var service = new PackageAiAssistant(chatClient: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DraftDescriptionAsync("Morning Pastry Box", "Surprise Bag", ["Vegetarian"]));

        Assert.Contains("Ollama:BaseUrl", ex.Message);
    }

    [Fact]
    public async Task DraftDescriptionAsync_ChatClientReturnsText_ReturnsTrimmedText()
    {
        var chatClient = new Mock<IChatClient>();
        chatClient
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "  A fresh box of pastries, saved from going to waste.  ")));
        var service = new PackageAiAssistant(chatClient.Object);

        var description = await service.DraftDescriptionAsync("Morning Pastry Box", "Surprise Bag", ["Vegetarian"]);

        Assert.Equal("A fresh box of pastries, saved from going to waste.", description);
    }

    [Fact]
    public async Task DraftDescriptionAsync_ChatClientThrows_WrapsInFriendlyError()
    {
        var chatClient = new Mock<IChatClient>();
        chatClient
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));
        var service = new PackageAiAssistant(chatClient.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DraftDescriptionAsync("Morning Pastry Box", "Surprise Bag", []));

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }
}
