using Microsoft.Extensions.AI;
using Moq;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Services.AI;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// Unlike PackageAiAssistant/SearchIntentParser, MarkdownPricingAgent wraps the mocked IChatClient
// in a real FunctionInvokingChatClient — so a mocked "model" turn that emits a
// FunctionCallContent really does invoke the real get_sell_through_history tool against the
// history list passed in. Only the mocked GetResponseAsync calls below are ever fabricated.
public class MarkdownPricingAgentTests
{
    [Fact]
    public async Task SuggestMarkdownAsync_NoChatClientConfigured_ThrowsFriendlyError()
    {
        var agent = new MarkdownPricingAgent();
        var package = TestData.Package(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => agent.SuggestMarkdownAsync(package, []));

        Assert.Contains("Ollama:BaseUrl", ex.Message);
    }

    [Fact]
    public async Task SuggestMarkdownAsync_ModelCallsToolThenSuggestsLowerPrice_ReturnsValidatedSuggestion()
    {
        var package = TestData.Package(Guid.NewGuid());
        package.Price = 10m;
        var history = new List<PackageSellThroughRecord>
        {
            new("Past Meal Box", package.PackageTypeId, "Meal Box", 8m, 5, 5, [], 3),
        };

        var chatClient = SequencedChatClient(
            ToolCallResponse("get_sell_through_history"),
            TextResponse("Ready to suggest a price."),
            TextResponse("""{"suggestedPrice":8,"explanation":"Similar boxes sold out fully at 8 RON."}"""));

        var agent = new MarkdownPricingAgent(chatClient.Object);

        var suggestion = await agent.SuggestMarkdownAsync(package, history);

        Assert.NotNull(suggestion);
        Assert.Equal(10m, suggestion!.CurrentPrice);
        Assert.Equal(8m, suggestion.SuggestedPrice);
        Assert.Contains("8 RON", suggestion.Explanation);
    }

    [Fact]
    public async Task SuggestMarkdownAsync_ModelSuggestsPriceAtOrAboveCurrent_ReturnsNull()
    {
        var package = TestData.Package(Guid.NewGuid());
        package.Price = 10m;

        var chatClient = SequencedChatClient(
            ToolCallResponse("get_sell_through_history"),
            TextResponse("Ready."),
            TextResponse("""{"suggestedPrice":10,"explanation":"No markdown needed."}"""));

        var agent = new MarkdownPricingAgent(chatClient.Object);

        var suggestion = await agent.SuggestMarkdownAsync(package, []);

        Assert.Null(suggestion);
    }

    [Fact]
    public async Task SuggestMarkdownAsync_ModelSuggestsAbsurdlyLowPrice_ClampsToFloor()
    {
        var package = TestData.Package(Guid.NewGuid());
        package.Price = 10m;

        var chatClient = SequencedChatClient(
            ToolCallResponse("get_sell_through_history"),
            TextResponse("Ready."),
            TextResponse("""{"suggestedPrice":0.10,"explanation":"Give it away."}"""));

        var agent = new MarkdownPricingAgent(chatClient.Object);

        var suggestion = await agent.SuggestMarkdownAsync(package, []);

        // MarkdownSettings.MinPriceFraction is 0.3 — never below 30% of the current price, even
        // when the model itself proposed something lower.
        Assert.NotNull(suggestion);
        Assert.Equal(3m, suggestion!.SuggestedPrice);
    }

    [Fact]
    public async Task SuggestMarkdownAsync_ModelSuggestsNegativeOrZeroPrice_ReturnsNull()
    {
        var package = TestData.Package(Guid.NewGuid());
        package.Price = 10m;

        var chatClient = SequencedChatClient(
            ToolCallResponse("get_sell_through_history"),
            TextResponse("Ready."),
            TextResponse("""{"suggestedPrice":0,"explanation":"Bad output."}"""));

        var agent = new MarkdownPricingAgent(chatClient.Object);

        var suggestion = await agent.SuggestMarkdownAsync(package, []);

        Assert.Null(suggestion);
    }

    [Fact]
    public async Task SuggestMarkdownAsync_ChatClientThrows_WrapsInFriendlyError()
    {
        var chatClient = new Mock<IChatClient>();
        chatClient
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var agent = new MarkdownPricingAgent(chatClient.Object);
        var package = TestData.Package(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => agent.SuggestMarkdownAsync(package, []));

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    private static Mock<IChatClient> SequencedChatClient(params ChatResponse[] responses)
    {
        var chatClient = new Mock<IChatClient>();
        var sequence = chatClient.SetupSequence(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()));
        foreach (var response in responses)
            sequence = sequence.ReturnsAsync(response);
        return chatClient;
    }

    private static ChatResponse ToolCallResponse(string toolName) =>
        new(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(Guid.NewGuid().ToString(), toolName, new Dictionary<string, object?>())]));

    private static ChatResponse TextResponse(string text) => new(new ChatMessage(ChatRole.Assistant, text));
}
