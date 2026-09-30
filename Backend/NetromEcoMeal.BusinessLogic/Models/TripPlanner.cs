namespace NetromEcoMeal.Models;

// Nearest-neighbor ordering over a customer's live pickup stops — plain geometry (GeoDistance),
// no routing API or AI. Kept independent of Entities so it's a plain, easily-unit-tested helper.
public static class TripPlanner
{
    public readonly record struct Stop(Guid Id, double Lat, double Lng);

    // Starts from (startLat, startLng) if given, else the first stop — then always hops to
    // whichever remaining stop is closest. Not a true TSP solve, just good enough for a few stops.
    public static List<Guid> PlanRoute(IReadOnlyList<Stop> stops, double? startLat, double? startLng)
    {
        if (stops.Count == 0)
            return [];

        var remaining = new List<Stop>(stops);
        var route = new List<Guid>(stops.Count);

        var current = startLat.HasValue && startLng.HasValue
            ? remaining.MinBy(s => GeoDistance.Km(startLat.Value, startLng.Value, s.Lat, s.Lng))
            : remaining[0];

        remaining.Remove(current);
        route.Add(current.Id);

        while (remaining.Count > 0)
        {
            var next = remaining.MinBy(s => GeoDistance.Km(current.Lat, current.Lng, s.Lat, s.Lng));
            remaining.Remove(next);
            route.Add(next.Id);
            current = next;
        }

        return route;
    }
}
