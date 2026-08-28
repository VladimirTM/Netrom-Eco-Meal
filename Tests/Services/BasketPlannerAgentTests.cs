using Microsoft.Extensions.AI;
using Moq;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services.AI;
using Netrom_Eco_Meal.Services.Interfaces;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// Unlike PackageAiAssistant/SearchIntentParser, BasketPlannerAgent wraps the mocked IChatClient in
// a real FunctionInvokingChatClient — so a mocked "model" turn that emits a FunctionCallContent
// really does invoke the real search tool against a mocked IPackageService. Only the mocked
// GetResponseAsync calls below are ever fabricated.
public class BasketPlannerAgentTests
{
    [Fact]
    public async Task ProposeBasketAsync_NoChatClientConfigured_ThrowsFriendlyError()
    {
        var packageService = new Mock<IPackageService>();
        var agent = new BasketPlannerAgent(packageService.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => agent.ProposeBasketAsync(4, 30m, null));

        Assert.Contains("Ollama:BaseUrl", ex.Message);
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(-1, 30)]
    [InlineData(4, 0)]
    [InlineData(4, -5)]
    public async Task ProposeBasketAsync_InvalidPeopleOrBudget_ThrowsFriendlyError(int peopleCount, decimal budget)
    {
        var packageService = new Mock<IPackageService>();
        var chatClient = new Mock<IChatClient>();
        var agent = new BasketPlannerAgent(packageService.Object, chatClient.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => agent.ProposeBasketAsync(peopleCount, budget, null));

        Assert.Contains("positive", ex.Message);
        // The chat client should never even be reached for input this obviously invalid.
        chatClient.Verify(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProposeBasketAsync_ModelCallsToolThenProposesRealPackage_ReturnsValidatedPlan()
    {
        var business = TestData.Business();
        var package = TestData.Package(business.Id, quantity: 5);
        package.Price = 12m;
        package.Business = business;

        var packageService = new Mock<IPackageService>();
        packageService.Setup(s => s.GetLiveCandidatesAsync(null)).ReturnsAsync([package]);

        var chatClient = SequencedChatClient(
            ToolCallResponse("search_live_packages"),
            TextResponse("Ready to propose a basket."),
            TextResponse($$"""{"items":[{"packageId":"{{package.Id}}","quantity":2,"reason":"Great value for the budget."}],"explanation":"A tasty pick."}"""));

        var agent = new BasketPlannerAgent(packageService.Object, chatClient.Object);

        var plan = await agent.ProposeBasketAsync(4, 30m, null);

        var item = Assert.Single(plan.Items);
        Assert.Equal(package.Id, item.Package.Id);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(24m, plan.TotalPrice);
        Assert.Equal("A tasty pick.", plan.Explanation);
        packageService.Verify(s => s.GetLiveCandidatesAsync(null), Times.Once);
    }

    [Fact]
    public async Task ProposeBasketAsync_ModelInventsPackageId_IsDropped()
    {
        var business = TestData.Business();
        var package = TestData.Package(business.Id);
        package.Business = business;

        var packageService = new Mock<IPackageService>();
        packageService.Setup(s => s.GetLiveCandidatesAsync(null)).ReturnsAsync([package]);

        var fabricatedId = Guid.NewGuid();
        var chatClient = SequencedChatClient(
            ToolCallResponse("search_live_packages"),
            TextResponse("Ready."),
            TextResponse($$"""{"items":[{"packageId":"{{fabricatedId}}","quantity":1,"reason":"Made up"}],"explanation":"Explanation."}"""));

        var agent = new BasketPlannerAgent(packageService.Object, chatClient.Object);

        var plan = await agent.ProposeBasketAsync(4, 30m, null);

        // A packageId the search tool never returned must never reach the final plan — a bad
        // extraction can only narrow the basket, never invent an item in it.
        Assert.Empty(plan.Items);
        Assert.Equal(0m, plan.TotalPrice);
    }

    [Fact]
    public async Task ProposeBasketAsync_QuantityAboveStock_IsClampedToAvailableStock()
    {
        var business = TestData.Business();
        var package = TestData.Package(business.Id, quantity: 3);
        package.Business = business;

        var packageService = new Mock<IPackageService>();
        packageService.Setup(s => s.GetLiveCandidatesAsync(null)).ReturnsAsync([package]);

        var chatClient = SequencedChatClient(
            ToolCallResponse("search_live_packages"),
            TextResponse("Ready."),
            TextResponse($$"""{"items":[{"packageId":"{{package.Id}}","quantity":99,"reason":"All of it"}],"explanation":"Explanation."}"""));

        var agent = new BasketPlannerAgent(packageService.Object, chatClient.Object);

        var plan = await agent.ProposeBasketAsync(4, 1000m, null);

        Assert.Equal(3, Assert.Single(plan.Items).Quantity);
    }

    [Fact]
    public async Task ProposeBasketAsync_ItemsSpanMultipleKitchens_KeepsOnlyTheHigherValueKitchen()
    {
        var businessA = TestData.Business();
        var businessB = TestData.Business();
        var packageA = TestData.Package(businessA.Id, quantity: 5);
        packageA.Price = 20m;
        packageA.Business = businessA;
        var packageB = TestData.Package(businessB.Id, quantity: 5);
        packageB.Price = 5m;
        packageB.Business = businessB;

        var packageService = new Mock<IPackageService>();
        packageService.Setup(s => s.GetLiveCandidatesAsync(null)).ReturnsAsync([packageA, packageB]);

        var chatClient = SequencedChatClient(
            ToolCallResponse("search_live_packages"),
            TextResponse("Ready."),
            TextResponse($$"""
                {"items":[
                    {"packageId":"{{packageA.Id}}","quantity":1,"reason":"Kitchen A pick"},
                    {"packageId":"{{packageB.Id}}","quantity":1,"reason":"Kitchen B pick"}
                ],"explanation":"Mixed basket."}
                """));

        var agent = new BasketPlannerAgent(packageService.Object, chatClient.Object);

        var plan = await agent.ProposeBasketAsync(4, 100m, null);

        // Orders can only span one business — the model mixed kitchens anyway, so the higher-value
        // kitchen (A: 20 RON) is kept over the cheaper one (B: 5 RON), and the explanation says so.
        var item = Assert.Single(plan.Items);
        Assert.Equal(packageA.Id, item.Package.Id);
        Assert.Contains("single kitchen", plan.Explanation);
    }

    [Fact]
    public async Task ProposeBasketAsync_TotalExceedsBudget_DropsLowestPriorityItemsUntilItFits()
    {
        var business = TestData.Business();
        var packageA = TestData.Package(business.Id, quantity: 5);
        packageA.Price = 20m;
        packageA.Business = business;
        var packageB = TestData.Package(business.Id, quantity: 5);
        packageB.Price = 20m;
        packageB.Business = business;

        var packageService = new Mock<IPackageService>();
        packageService.Setup(s => s.GetLiveCandidatesAsync(null)).ReturnsAsync([packageA, packageB]);

        var chatClient = SequencedChatClient(
            ToolCallResponse("search_live_packages"),
            TextResponse("Ready."),
            TextResponse($$"""
                {"items":[
                    {"packageId":"{{packageA.Id}}","quantity":1,"reason":"First pick"},
                    {"packageId":"{{packageB.Id}}","quantity":1,"reason":"Second pick"}
                ],"explanation":"Two boxes."}
                """));

        var agent = new BasketPlannerAgent(packageService.Object, chatClient.Object);

        // Budget only fits one 20 RON item, not both.
        var plan = await agent.ProposeBasketAsync(2, 25m, null);

        Assert.Single(plan.Items);
        Assert.True(plan.TotalPrice <= 25m);
    }

    [Fact]
    public async Task ProposeBasketAsync_BudgetTrimEmptiesBasket_ShowsNothingFitInsteadOfStaleExplanation()
    {
        // Found against the real qwen2.5:7b model: too low a budget still gets an explanation
        // describing the items the budget trim then drops — that text must not survive.
        var business = TestData.Business();
        var package = TestData.Package(business.Id, quantity: 5);
        package.Price = 20m;
        package.Business = business;

        var packageService = new Mock<IPackageService>();
        packageService.Setup(s => s.GetLiveCandidatesAsync(null)).ReturnsAsync([package]);

        var chatClient = SequencedChatClient(
            ToolCallResponse("search_live_packages"),
            TextResponse("Ready."),
            TextResponse($$"""{"items":[{"packageId":"{{package.Id}}","quantity":1,"reason":"Best fit"}],"explanation":"A 20 RON pick for your basket."}"""));

        var agent = new BasketPlannerAgent(packageService.Object, chatClient.Object);

        var plan = await agent.ProposeBasketAsync(4, 3m, null);

        Assert.Empty(plan.Items);
        Assert.DoesNotContain("20 RON pick", plan.Explanation);
        Assert.Contains("No live packages matched", plan.Explanation);
    }

    [Fact]
    public async Task ProposeBasketAsync_ChatClientThrows_WrapsInFriendlyError()
    {
        var packageService = new Mock<IPackageService>();
        var chatClient = new Mock<IChatClient>();
        chatClient
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var agent = new BasketPlannerAgent(packageService.Object, chatClient.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => agent.ProposeBasketAsync(4, 30m, null));

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
