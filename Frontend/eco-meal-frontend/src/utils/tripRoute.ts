// Ports NetromEcoMeal.Models.TripPlanner.PlanRoute + GeoDistance.Km — nearest-neighbor ordering
// over a customer's live pickup stops, plain haversine geometry, no routing API or AI. Not a
// true TSP solve, just good enough for a handful of stops (same comment as the C# original).

export interface TripStop {
  id: string;
  lat: number;
  lng: number;
}

const EARTH_RADIUS_KM = 6371;

function toRadians(degrees: number): number {
  return (degrees * Math.PI) / 180;
}

export function haversineKm(lat1: number, lng1: number, lat2: number, lng2: number): number {
  const dLat = toRadians(lat2 - lat1);
  const dLng = toRadians(lng2 - lng1);
  const a =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(toRadians(lat1)) * Math.cos(toRadians(lat2)) * Math.sin(dLng / 2) * Math.sin(dLng / 2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return EARTH_RADIUS_KM * c;
}

function minBy<T>(items: T[], key: (item: T) => number): T {
  let best = items[0];
  let bestKey = key(best);
  for (let i = 1; i < items.length; i++) {
    const k = key(items[i]);
    if (k < bestKey) {
      best = items[i];
      bestKey = k;
    }
  }
  return best;
}

// Starts from (startLat, startLng) if given, else the first stop — then always hops to whichever
// remaining stop is closest.
export function planRoute(stops: TripStop[], startLat: number | null, startLng: number | null): string[] {
  if (stops.length === 0) return [];

  const remaining = [...stops];
  const route: string[] = [];

  let current =
    startLat !== null && startLng !== null
      ? minBy(remaining, (s) => haversineKm(startLat, startLng, s.lat, s.lng))
      : remaining[0];

  remaining.splice(remaining.indexOf(current), 1);
  route.push(current.id);

  while (remaining.length > 0) {
    const next = minBy(remaining, (s) => haversineKm(current.lat, current.lng, s.lat, s.lng));
    remaining.splice(remaining.indexOf(next), 1);
    route.push(next.id);
    current = next;
  }

  return route;
}
