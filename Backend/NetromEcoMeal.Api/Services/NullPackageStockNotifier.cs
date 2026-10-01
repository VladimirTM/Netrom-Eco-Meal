using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Services;

// Interim IPackageStockNotifier for the Api host: PackageService/OrderService need *an*
// implementation to resolve regardless of how live stock reaches clients, but the real one —
// a SignalR hub at /hubs/stock — is explicitly Phase 5's task, not
// Phase 3's. Until then, a change made through the Api simply doesn't push a live update; polling
// or a page refresh still sees it, same as before Phase 5 lands.
public class NullPackageStockNotifier : IPackageStockNotifier
{
    public void NotifyBusinessChanged(Guid businessId)
    {
    }
}
