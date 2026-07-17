using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class OrderController(IOrderService orderService) : ControllerBase
{
    public async Task<ActionResult<Order>> PlaceOrderAsync(Guid businessId, List<OrderLineRequest> lines)
    {
        try
        {
            return await orderService.PlaceOrderAsync(businessId, lines);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            return Conflict(ex.Message);
        }
    }

    public async Task<ActionResult<List<Order>>> GetMyOrdersAsync()
    {
        try
        {
            return await orderService.GetMyOrdersAsync();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    public async Task<ActionResult<List<Order>>> GetOrdersForManagementAsync()
    {
        try
        {
            return await orderService.GetOrdersForManagementAsync();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    public async Task<ActionResult<PaginatedList<Order>>> GetMyOrdersPagedAsync(int pageIndex, int pageSize, string? status)
    {
        try
        {
            return await orderService.GetMyOrdersPagedAsync(pageIndex, pageSize, status);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    public async Task<ActionResult<PaginatedList<Order>>> GetOrdersForManagementPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessId, string? status)
    {
        try
        {
            return await orderService.GetOrdersForManagementPagedAsync(pageIndex, pageSize, search, businessId, status);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    public async Task<ActionResult<Order>> UpdateOrderStatusAsync(Guid orderId, string statusName)
    {
        try
        {
            return await orderService.UpdateStatusAsync(orderId, statusName);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            return Conflict(ex.Message);
        }
    }

    public async Task<ActionResult<Order>> CancelMyOrderAsync(Guid orderId)
    {
        try
        {
            return await orderService.CancelMyOrderAsync(orderId);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            return Conflict(ex.Message);
        }
    }

    public async Task<ActionResult<Order>> GetMyOrderAsync(Guid orderId)
    {
        try
        {
            return await orderService.GetMyOrderAsync(orderId);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    public async Task<ActionResult<Order>> GetOrderForManagementAsync(Guid orderId)
    {
        try
        {
            return await orderService.GetOrderForManagementAsync(orderId);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }
}
