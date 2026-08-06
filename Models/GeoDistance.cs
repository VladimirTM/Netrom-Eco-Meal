namespace Netrom_Eco_Meal.Models;

// Haversine great-circle distance — computed in-memory (not translated to SQL) since the
// dataset is small and Npgsql's Math function translation for this shape isn't reliable.
public static class GeoDistance
{
    private const double EarthRadiusKm = 6371;

    public static double Km(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLng = ToRadians(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
