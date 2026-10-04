namespace NetromEcoMeal.Services.Interfaces;

// Lets PackageService/OrderService announce a stock change without depending on how subscribers
// are notified. SignalRPackageStockNotifier (NetromEcoMeal.Api) pushes it over /hubs/stock.
public interface IPackageStockNotifier
{
    public void NotifyBusinessChanged(Guid businessId);
}
