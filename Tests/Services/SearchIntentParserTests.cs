using Microsoft.Extensions.AI;
using Moq;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Services.AI;

namespace Netrom_Eco_Meal.Tests.Services;

// Same shape as PackageAiAssistantTests — IChatClient is mocked, never a real Ollama instance.
public class SearchIntentParserTests
{
    [Fact]
    public async Task ParseAsync_NoChatClientConfigured_ThrowsFriendlyError()
    {
        var parser = new SearchIntentParser(chatClient: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => parser.ParseAsync("vegan dinner"));

        Assert.Contains("Ollama:BaseUrl", ex.Message);
    }

    [Fact]
    public async Task ParseAsync_ValidJson_ReturnsParsedIntent()
    {
        var chatClient = new Mock<IChatClient>();
        chatClient
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                """{"keywords":"dinner","dietaryTag":"Vegan","maxPrice":30,"closingSoon":true,"nearMe":false}""")));
        var parser = new SearchIntentParser(chatClient.Object);

        var intent = await parser.ParseAsync("vegan dinner under 30 lei closing soon");

        Assert.Equal("dinner", intent.Keywords);
        Assert.Equal(DietaryTags.Vegan, intent.DietaryTag);
        Assert.Equal(30, intent.MaxPrice);
        Assert.True(intent.ClosingSoon);
        Assert.False(intent.NearMe);
    }

    [Fact]
    public async Task ParseAsync_UnknownDietaryTag_IsDropped()
    {
        var chatClient = new Mock<IChatClient>();
        chatClient
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                """{"keywords":null,"dietaryTag":"Keto","maxPrice":null,"closingSoon":false,"nearMe":false}""")));
        var parser = new SearchIntentParser(chatClient.Object);

        var intent = await parser.ParseAsync("keto lunch");

        // "Keto" isn't in Constants.DietaryTags.All — a fabricated tag must never reach the
        // filter, only ever narrow to real, known values.
        Assert.Null(intent.DietaryTag);
    }

    [Fact]
    public async Task ParseAsync_NegativeMaxPrice_IsDropped()
    {
        var chatClient = new Mock<IChatClient>();
        chatClient
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                """{"keywords":null,"dietaryTag":null,"maxPrice":-5,"closingSoon":false,"nearMe":false}""")));
        var parser = new SearchIntentParser(chatClient.Object);

        var intent = await parser.ParseAsync("free food please");

        Assert.Null(intent.MaxPrice);
    }

    [Fact]
    public async Task ParseAsync_ChatClientThrows_WrapsInFriendlyError()
    {
        var chatClient = new Mock<IChatClient>();
        chatClient
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));
        var parser = new SearchIntentParser(chatClient.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => parser.ParseAsync("vegan dinner"));

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task ParseAsync_InvalidJson_ThrowsFriendlyError()
    {
        var chatClient = new Mock<IChatClient>();
        chatClient
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "not json")));
        var parser = new SearchIntentParser(chatClient.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => parser.ParseAsync("vegan dinner"));
    }
}
