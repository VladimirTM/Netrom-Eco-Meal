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
    // businessId is only honored for admins — managers are always scoped to their own business.
    public Task<List<Order>> GetOrdersInRangeAsync(DateTime? from, DateTime? to, Guid? businessId = null);
    public Task<Order> UpdateStatusAsync(Guid orderId, string statusName);
    public Task<Order> GetOrderForManagementAsync(Guid orderId);
    public Task<Order> GetMyOrderAsync(Guid orderId);
    public Task<Order> CancelMyOrderAsync(Guid orderId);
    public Task<decimal> GetTotalKgSavedAsync();
    // System-triggered, no current-user auth — called by the background expiry sweep.
    public Task<int> ExpireStalePendingOrdersAsync();
    // Public stock info (same audience as package browsing) — no auth restriction.
    public Task<Dictionary<Guid, int>> GetPendingReservedQuantitiesAsync(IEnumerable<Guid> packageIds);
}
