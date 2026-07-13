using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

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
}
