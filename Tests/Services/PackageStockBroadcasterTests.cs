using Netrom_Eco_Meal.Services;

namespace Netrom_Eco_Meal.Tests.Services;

// Covers the singleton pub/sub itself, not the OrderService/PackageService call sites that raise
// it (see PlaceOrderAsync_Success_BroadcastsBusinessChanged etc. in those services' own test files).
public class PackageStockBroadcasterTests
{
    [Fact]
    public void NotifyBusinessChanged_NoSubscribers_DoesNotThrow()
    {
        var broadcaster = new PackageStockBroadcaster();

        broadcaster.NotifyBusinessChanged(Guid.NewGuid());
    }

    [Fact]
    public void NotifyBusinessChanged_MultipleSubscribers_AllReceiveIt()
    {
        var broadcaster = new PackageStockBroadcaster();
        var businessId = Guid.NewGuid();
        var firstReceived = new List<Guid>();
        var secondReceived = new List<Guid>();
        broadcaster.BusinessStockChanged += firstReceived.Add;
        broadcaster.BusinessStockChanged += secondReceived.Add;

        broadcaster.NotifyBusinessChanged(businessId);

        Assert.Equal([businessId], firstReceived);
        Assert.Equal([businessId], secondReceived);
    }

    [Fact]
    public void NotifyBusinessChanged_AfterUnsubscribe_DoesNotReachThatSubscriber()
    {
        var broadcaster = new PackageStockBroadcaster();
        var received = new List<Guid>();
        void Handler(Guid businessId) => received.Add(businessId);
        broadcaster.BusinessStockChanged += Handler;
        broadcaster.BusinessStockChanged -= Handler;

        broadcaster.NotifyBusinessChanged(Guid.NewGuid());

        Assert.Empty(received);
    }
}
