using Netrom_Eco_Meal.Models;

namespace Netrom_Eco_Meal.Tests.Models;

// Pure-logic coverage for the nearest-neighbor pickup-route ordering behind /trip-planner — no
// DbContext involved, just geometry over already-loaded Business.Latitude/Longitude pairs.
public class TripPlannerTests
{
    [Fact]
    public void PlanRoute_NoStops_ReturnsEmpty()
    {
        var route = TripPlanner.PlanRoute([], null, null);

        Assert.Empty(route);
    }

    [Fact]
    public void PlanRoute_NoStartLocation_StartsFromFirstStopThenNearestNeighbor()
    {
        // Roughly a west-to-east line: A(0,0) --far-- C(0,10) --near-- B(0,10.1)
        var a = new TripPlanner.Stop(Guid.NewGuid(), 0, 0);
        var b = new TripPlanner.Stop(Guid.NewGuid(), 0, 10.1);
        var c = new TripPlanner.Stop(Guid.NewGuid(), 0, 10);
        var stops = new List<TripPlanner.Stop> { a, b, c };

        var route = TripPlanner.PlanRoute(stops, null, null);

        // Starts at A (first in the input); C is nearer to A than B, so C comes next, then B.
        Assert.Equal([a.Id, c.Id, b.Id], route);
    }

    [Fact]
    public void PlanRoute_WithStartLocation_StartsFromNearestStopToIt()
    {
        var far = new TripPlanner.Stop(Guid.NewGuid(), 0, 20);
        var near = new TripPlanner.Stop(Guid.NewGuid(), 0, 1);
        var stops = new List<TripPlanner.Stop> { far, near };

        // Customer's own position is right next to "near".
        var route = TripPlanner.PlanRoute(stops, 0, 0.9);

        Assert.Equal([near.Id, far.Id], route);
    }

    [Fact]
    public void PlanRoute_SingleStop_ReturnsThatStop()
    {
        var only = new TripPlanner.Stop(Guid.NewGuid(), 45.75, 21.22);

        var route = TripPlanner.PlanRoute([only], null, null);

        Assert.Equal([only.Id], route);
    }
}
