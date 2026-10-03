using Microsoft.AspNetCore.SignalR;

namespace NetromEcoMeal.Api.Hubs;

// Live-stock push for BusinessDetail — anonymous and authenticated visitors alike join a
// business's group to hear about it the moment PackageService/OrderService change its stock, no
// polling needed. No [Authorize]: browsing a storefront page needs no login (same as the REST
// GetById/GetPaged routes it rides alongside), and the group itself carries no sensitive data —
// only "this businessId's stock changed," never quantities or package contents.
public class StockHub : Hub
{
    public Task JoinBusinessGroup(Guid businessId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(businessId));

    public Task LeaveBusinessGroup(Guid businessId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(businessId));

    public static string GroupName(Guid businessId) => $"business:{businessId}";
}
