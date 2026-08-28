using Microsoft.Extensions.AI;
using Moq;
using Netrom_Eco_Meal.Services.AI;

namespace Netrom_Eco_Meal.Tests.Services;

// Same shape as PackageAiAssistantTests — IChatClient itself is never exercised against a real
// Ollama instance here, just the "no client configured -> friendly error" degradation and the
// happy-path prompt/response plumbing.
public class NearExpiryNudgeComposerTests
{
    [Fact]
    public async Task ComposeAsync_NoChatClientConfigured_ThrowsFriendlyError()
    {
        var service = new NearExpiryNudgeComposer(chatClient: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ComposeAsync("Golden Boot Surprise Bag", "Stadionul de Gusturi", 2, TimeSpan.FromMinutes(20), null));

        Assert.Contains("Ollama:BaseUrl", ex.Message);
    }

    [Fact]
    public async Task ComposeAsync_ChatClientReturnsText_ReturnsTrimmedText()
    {
        var chatClient = new Mock<IChatClient>();
        chatClient
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "  2 portions left at Stadionul de Gusturi, closing in 20 minutes.  ")));
        var service = new NearExpiryNudgeComposer(chatClient.Object);

        var message = await service.ComposeAsync("Golden Boot Surprise Bag", "Stadionul de Gusturi", 2, TimeSpan.FromMinutes(20), null);

        Assert.Equal("2 portions left at Stadionul de Gusturi, closing in 20 minutes.", message);
    }

    [Fact]
    public async Task ComposeAsync_ChatClientThrows_WrapsInFriendlyError()
    {
        var chatClient = new Mock<IChatClient>();
        chatClient
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));
        var service = new NearExpiryNudgeComposer(chatClient.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ComposeAsync("Golden Boot Surprise Bag", "Stadionul de Gusturi", 2, TimeSpan.FromMinutes(20), "Vegan"));

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }
}
