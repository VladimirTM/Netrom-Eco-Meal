import L from "leaflet";
import "leaflet/dist/leaflet.css";
import markerIcon2x from "leaflet/dist/images/marker-icon-2x.png";
import markerIcon from "leaflet/dist/images/marker-icon.png";
import markerShadow from "leaflet/dist/images/marker-shadow.png";
import { useCallback, useEffect, useRef } from "react";

// Vite doesn't rewrite the relative URLs Leaflet's default CSS bakes in for its marker icons, so
// the default pins render blank unless these are set explicitly — a well-known Leaflet+bundler gap.
let iconsConfigured = false;
function configureDefaultIcon() {
  if (iconsConfigured) return;
  iconsConfigured = true;
  L.Icon.Default.mergeOptions({ iconRetinaUrl: markerIcon2x, iconUrl: markerIcon, shadowUrl: markerShadow });
}

export interface MapMarker {
  id: string;
  name: string;
  lat: number;
  lng: number;
}

// Timișoara — same seed-data-city fallback as site.js's EcoMeal.map.render.
const DEFAULT_CENTER: [number, number] = [45.7489, 21.2087];

function escapeHtml(value: string): string {
  const entities: Record<string, string> = { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" };
  return value.replace(/[&<>"']/g, (c) => entities[c]);
}

function renderMarkers(map: L.Map, markers: MapMarker[], onMarkerClick?: (id: string) => void) {
  const layerGroup = L.layerGroup().addTo(map);
  const bounds: [number, number][] = [];
  for (const marker of markers) {
    const leafletMarker = L.marker([marker.lat, marker.lng]).addTo(layerGroup);
    leafletMarker.bindPopup(`<strong>${escapeHtml(marker.name)}</strong>`);
    leafletMarker.on("click", () => onMarkerClick?.(marker.id));
    bounds.push([marker.lat, marker.lng]);
  }

  if (bounds.length > 0) map.fitBounds(bounds, { padding: [30, 30] });
  else map.setView(DEFAULT_CENTER, 12);

  return layerGroup;
}

// Replaces site.js's EcoMeal.map.render/destroy JS interop with a vanilla-Leaflet React hook —
// not react-leaflet, to keep the dependency surface the same as the Blazor version's. The map
// instance and its markers are torn down on unmount/re-render so repeated mounts (e.g. toggling
// Home's map view off and on) never leak a Leaflet instance.
//
// Returns a callback ref rather than taking a RefObject: the container is often conditionally
// rendered behind a loading state (Home's "fetching businesses" spinner, Trip Planner's "fetching
// orders" spinner), so the DOM node frequently doesn't exist yet on the render that first enables
// the map. A RefObject's identity never changes when its .current is filled in later, so an effect
// keyed on `[containerRef]` only ever runs once, before the real node exists, and silently never
// creates the map. A callback ref is invoked by React exactly when the node mounts/unmounts, which
// is the actual event this needs to react to.
export function useLeafletMap(markers: MapMarker[], onMarkerClick?: (id: string) => void) {
  const mapRef = useRef<L.Map | null>(null);
  const layerGroupRef = useRef<L.LayerGroup | null>(null);
  // Mirrored into refs so the containerRef callback below (invoked by React outside of render,
  // on actual DOM mount/unmount) always sees the latest values without needing to be recreated.
  const markersRef = useRef(markers);
  // oxlint-disable-next-line react/refs
  markersRef.current = markers;
  const onMarkerClickRef = useRef(onMarkerClick);
  // oxlint-disable-next-line react/refs
  onMarkerClickRef.current = onMarkerClick;

  const containerRef = useCallback((node: HTMLElement | null) => {
    if (mapRef.current) {
      mapRef.current.remove();
      mapRef.current = null;
      layerGroupRef.current = null;
    }
    if (!node) return;

    configureDefaultIcon();
    const map = L.map(node);
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      attribution: "&copy; OpenStreetMap contributors",
      maxZoom: 19,
    }).addTo(map);
    mapRef.current = map;
    layerGroupRef.current = renderMarkers(map, markersRef.current, onMarkerClickRef.current);
  }, []);

  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;

    layerGroupRef.current?.remove();
    layerGroupRef.current = renderMarkers(map, markers, onMarkerClickRef.current);

    return () => {
      layerGroupRef.current?.remove();
    };
  }, [markers]);

  return containerRef;
}
