using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/notifications")]
[ApiController]
[Authorize]
public class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<List<NotificationDto>>> GetMyNotifications([FromQuery] int take = 20) =>
        Ok((await notificationService.GetMyNotificationsAsync(take)).Select(NotificationDto.FromEntity).ToList());

    [HttpGet("mine/unread-count")]
    public async Task<ActionResult<int>> GetMyUnreadCount() =>
        Ok(await notificationService.GetMyUnreadCountAsync());

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        await notificationService.MarkAsReadAsync(id);
        return NoContent();
    }

    [HttpPost("mine/read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await notificationService.MarkAllAsReadAsync();
        return NoContent();
    }
}
