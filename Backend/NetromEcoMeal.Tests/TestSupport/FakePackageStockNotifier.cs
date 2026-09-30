using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Tests.TestSupport;

// Mirrors the Web host's PackageStockBroadcaster (a plain C# event) without the Tests project
// needing a reference to Web — PackageService/OrderService depend only on IPackageStockNotifier.
public class FakePackageStockNotifier : IPackageStockNotifier
{
    public event Action<Guid>? BusinessStockChanged;

    public void NotifyBusinessChanged(Guid businessId) => BusinessStockChanged?.Invoke(businessId);
}
