using Microsoft.AspNetCore.SignalR;
using Moq;
using NetromEcoMeal.Api.Hubs;
using NetromEcoMeal.Api.Services;

namespace NetromEcoMeal.Tests.Api;

// Pure unit test (no TestServer/real WebSocket needed) — confirms the Api host's
// IPackageStockNotifier implementation pushes to the right SignalR group, the one thing that
// can't be inferred from the interface shape alone. A manual two-browser live-stock
// check covers the real end-to-end wire behavior.
public class SignalRPackageStockNotifierTests
{
    [Fact]
    public void NotifyBusinessChanged_SendsToTheBusinessSpecificGroup()
    {
        var businessId = Guid.NewGuid();
        var clientProxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(StockHub.GroupName(businessId))).Returns(clientProxy.Object);
        var hubContext = new Mock<IHubContext<StockHub>>();
        hubContext.Setup(h => h.Clients).Returns(clients.Object);

        new SignalRPackageStockNotifier(hubContext.Object).NotifyBusinessChanged(businessId);

        clients.Verify(c => c.Group($"business:{businessId}"), Times.Once);
        clientProxy.Verify(p => p.SendCoreAsync("BusinessStockChanged", It.Is<object[]>(args => args.Length == 1 && (Guid)args[0] == businessId), default), Times.Once);
    }
}
