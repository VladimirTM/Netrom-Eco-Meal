using Microsoft.AspNetCore.Identity;

namespace Netrom_Eco_Meal.Entities;

// Extends Identity's built-in user with the display name Identity doesn't provide.
public class ApplicationUser : IdentityUser
{
    public required string Name { get; set; }
    public ICollection<Order> Orders { get; set; } = [];
}