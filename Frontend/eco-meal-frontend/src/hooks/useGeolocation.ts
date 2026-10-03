import { useCallback, useState } from "react";

export interface GeoPosition {
  lat: number;
  lng: number;
}

// Mirrors site.js's EcoMeal.geo.getPosition plus TripPlanner.razor's own belt-and-suspenders
// wrapper around it (the 2026-09-07 "Trip Planner geo hang" fix) — the browser API gets an 8s
// timeout option, and a hard 10s client-side timeout on top guarantees this promise always
// settles even if a browser/extension never calls either geolocation callback at all. Always
// degrades to null, never rejects or hangs — callers never need a try/catch.
function getPositionOnce(): Promise<GeoPosition | null> {
  return new Promise((resolve) => {
    if (!navigator.geolocation) {
      resolve(null);
      return;
    }

    let settled = false;
    const settle = (value: GeoPosition | null) => {
      if (settled) return;
      settled = true;
      resolve(value);
    };

    navigator.geolocation.getCurrentPosition(
      (position) => settle({ lat: position.coords.latitude, lng: position.coords.longitude }),
      () => settle(null),
      { timeout: 8000 },
    );
    setTimeout(() => settle(null), 10000);
  });
}

export function useGeolocation() {
  const [locating, setLocating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const locate = useCallback(async (): Promise<GeoPosition | null> => {
    setLocating(true);
    setError(null);
    const position = await getPositionOnce();
    setLocating(false);
    if (!position) setError("Couldn't get your location — check your browser's location permission.");
    return position;
  }, []);

  return { locate, locating, error };
}
