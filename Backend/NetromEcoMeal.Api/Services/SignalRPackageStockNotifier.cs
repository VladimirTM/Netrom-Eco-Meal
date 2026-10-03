using Microsoft.AspNetCore.SignalR;
using NetromEcoMeal.Api.Hubs;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Services;

// The real IPackageStockNotifier for the Api host — pushes
// over /hubs/stock instead of Web's in-process C# event, so a React BusinessDetail page and every
// other open tab/circuit across both hosts see a stock change without polling or a reload.
public class SignalRPackageStockNotifier(IHubContext<StockHub> hubContext) : IPackageStockNotifier
{
    public void NotifyBusinessChanged(Guid businessId) =>
        // Fire-and-forget: NotifyBusinessChanged is synchronous, and a failed push isn't worth
        // failing the request that changed the stock — clients catch up on their next fetch.
        _ = hubContext.Clients.Group(StockHub.GroupName(businessId)).SendAsync("BusinessStockChanged", businessId);
}
