using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Models;

// Backs the public /brands/{id} page — Brand plus its public locations and an aggregated rating.
public class BrandDetailDto
{
    public required Brand Brand { get; set; }
    public required List<Business> Locations { get; set; }
    public double? AverageRating { get; set; }
    public int RatingCount { get; set; }
}
