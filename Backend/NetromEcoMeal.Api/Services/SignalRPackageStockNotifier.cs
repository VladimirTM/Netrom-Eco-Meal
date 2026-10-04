using Microsoft.AspNetCore.SignalR;
using NetromEcoMeal.Api.Hubs;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Services;

// Pushes stock changes over /hubs/stock so every open BusinessDetail tab subscribed to a
// business's group updates without polling or a reload.
public class SignalRPackageStockNotifier(IHubContext<StockHub> hubContext) : IPackageStockNotifier
{
    public void NotifyBusinessChanged(Guid businessId) =>
        // Fire-and-forget: NotifyBusinessChanged is synchronous, and a failed push isn't worth
        // failing the request that changed the stock — clients catch up on their next fetch.
        _ = hubContext.Clients.Group(StockHub.GroupName(businessId)).SendAsync("BusinessStockChanged", businessId);
}
