namespace NetromEcoMeal.Services.Interfaces;

// Lets PackageService/OrderService announce a stock change without depending on how subscribers
// are notified. The Web implementation is a plain C# event (today's Blazor circuits); the Api
// implementation (Phase 5) will push over a SignalR hub instead.
public interface IPackageStockNotifier
{
    public void NotifyBusinessChanged(Guid businessId);
}
