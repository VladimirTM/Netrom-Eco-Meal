using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Models;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/orders")]
[ApiController]
[Authorize]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrderDto>> PlaceOrder([FromBody] PlaceOrderRequestDto request)
    {
        var order = await orderService.PlaceOrderAsync(request.BusinessId, request.Lines, request.LogisticsNote);
        return CreatedAtAction(nameof(GetMyOrder), new { orderId = order.Id }, OrderDto.FromEntity(order));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<List<OrderDto>>> GetMyOrders() =>
        Ok((await orderService.GetMyOrdersAsync()).Select(OrderDto.FromEntity).ToList());

    [HttpGet("mine/paged")]
    public async Task<ActionResult<PaginatedList<OrderDto>>> GetMyOrdersPaged(
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null)
    {
        var page = await orderService.GetMyOrdersPagedAsync(pageIndex, pageSize, status);
        return Ok(page.MapItems(OrderDto.FromEntity));
    }

    [HttpGet("mine/{orderId:guid}")]
    public async Task<ActionResult<OrderDto>> GetMyOrder(Guid orderId) =>
        Ok(OrderDto.FromEntity(await orderService.GetMyOrderAsync(orderId)));

    [HttpGet("manage")]
    public async Task<ActionResult<List<OrderDto>>> GetOrdersForManagement([FromQuery] Guid? businessId = null) =>
        Ok((await orderService.GetOrdersForManagementAsync(businessId)).Select(OrderDto.FromEntity).ToList());

    [HttpGet("manage/paged")]
    public async Task<ActionResult<PaginatedList<OrderDto>>> GetOrdersForManagementPaged(
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] Guid? businessId = null, [FromQuery] string? status = null)
    {
        var page = await orderService.GetOrdersForManagementPagedAsync(pageIndex, pageSize, search, businessId, status);
        return Ok(page.MapItems(OrderDto.FromEntity));
    }

    [HttpGet("manage/{orderId:guid}")]
    public async Task<ActionResult<OrderDto>> GetOrderForManagement(Guid orderId) =>
        Ok(OrderDto.FromEntity(await orderService.GetOrderForManagementAsync(orderId)));

    [HttpGet("range")]
    public async Task<ActionResult<List<OrderDto>>> GetOrdersInRange([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? businessId = null) =>
        Ok((await orderService.GetOrdersInRangeAsync(from, to, businessId)).Select(OrderDto.FromEntity).ToList());

    [HttpPut("{orderId:guid}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(Guid orderId, [FromBody] UpdateOrderStatusRequestDto request) =>
        Ok(OrderDto.FromEntity(await orderService.UpdateStatusAsync(orderId, request.StatusName)));

    [HttpPost("{orderId:guid}/cancel")]
    public async Task<ActionResult<OrderDto>> CancelMyOrder(Guid orderId) =>
        Ok(OrderDto.FromEntity(await orderService.CancelMyOrderAsync(orderId)));

    [HttpPost("{orderId:guid}/split-passes")]
    public async Task<ActionResult<OrderDto>> SplitPickupPasses(Guid orderId, [FromBody] SplitPickupPassesRequestDto request) =>
        Ok(OrderDto.FromEntity(await orderService.SplitPickupPassesAsync(orderId, request.PassCount)));

    [HttpPost("{orderId:guid}/passes/{passId:guid}/redeem")]
    public async Task<ActionResult<OrderDto>> RedeemPickupPass(Guid orderId, Guid passId) =>
        Ok(OrderDto.FromEntity(await orderService.RedeemPickupPassAsync(orderId, passId)));

    [HttpGet("impact/kg-saved")]
    [AllowAnonymous]
    public async Task<ActionResult<decimal>> GetTotalKgSaved() =>
        Ok(await orderService.GetTotalKgSavedAsync());

    [HttpGet("reserved-quantities")]
    [AllowAnonymous]
    public async Task<ActionResult<Dictionary<Guid, int>>> GetPendingReservedQuantities([FromQuery] List<Guid> packageIds) =>
        Ok(await orderService.GetPendingReservedQuantitiesAsync(packageIds));
}
