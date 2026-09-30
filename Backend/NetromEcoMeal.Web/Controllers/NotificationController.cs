using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Controllers;

// Also registered as a scoped service and injected directly into Razor pages, bypassing HTTP.
[ApiController]
[Route("/")]
public class NotificationController(INotificationService notificationService) : ControllerBase
{
    public async Task<ActionResult<List<Notification>>> GetMyNotificationsAsync(int take = 20)
    {
        return await notificationService.GetMyNotificationsAsync(take);
    }

    public async Task<ActionResult<int>> GetMyUnreadCountAsync()
    {
        return await notificationService.GetMyUnreadCountAsync();
    }

    public async Task<ActionResult> MarkAsReadAsync(Guid id)
    {
        await notificationService.MarkAsReadAsync(id);
        return NoContent();
    }

    public async Task<ActionResult> MarkAllAsReadAsync()
    {
        await notificationService.MarkAllAsReadAsync();
        return NoContent();
    }
}
