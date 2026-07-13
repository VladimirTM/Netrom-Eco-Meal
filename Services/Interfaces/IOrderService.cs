using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

public record OrderLineRequest(Guid PackageId, int Quantity);

public interface IOrderService
{
    public Task<Order> PlaceOrderAsync(Guid businessId, List<OrderLineRequest> lines);
    public Task<List<Order>> GetMyOrdersAsync();
    public Task<List<Order>> GetOrdersForManagementAsync();
    public Task<Order> UpdateStatusAsync(Guid orderId, string statusName);
}
