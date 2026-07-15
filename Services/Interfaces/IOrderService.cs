using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;

namespace Netrom_Eco_Meal.Services.Interfaces;

public record OrderLineRequest(Guid PackageId, int Quantity);

// Order placement is customer-only; management is admin or the order's own business manager.
public interface IOrderService
{
    public Task<Order> PlaceOrderAsync(Guid businessId, List<OrderLineRequest> lines);
    public Task<List<Order>> GetMyOrdersAsync();
    public Task<List<Order>> GetOrdersForManagementAsync();
    public Task<PaginatedList<Order>> GetMyOrdersPagedAsync(int pageIndex, int pageSize, string? status);
    public Task<PaginatedList<Order>> GetOrdersForManagementPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessId, string? status);
    public Task<Order> UpdateStatusAsync(Guid orderId, string statusName);
    public Task<Order> CancelMyOrderAsync(Guid orderId);
}
