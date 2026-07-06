using Microsoft.AspNetCore.Identity;

namespace Netrom_Eco_Meal.Entities;

public class ApplicationUser : IdentityUser
{
    public required string Name { get; set; }
    public ICollection<Order> Orders { get; set; } = [];
}