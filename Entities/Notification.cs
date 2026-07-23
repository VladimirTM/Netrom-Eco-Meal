using System.ComponentModel.DataAnnotations.Schema;

namespace Netrom_Eco_Meal.Entities;

// In-app notification for order lifecycle events — created server-side, read by the bell in the header.
public class Notification
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required string Message { get; set; }
    // Relative app URL the notification should open when clicked, e.g. "/orders".
    public string? Url { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;
}
