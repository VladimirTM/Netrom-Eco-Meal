import L from "leaflet";
import "leaflet/dist/leaflet.css";
import markerIcon2x from "leaflet/dist/images/marker-icon-2x.png";
import markerIcon from "leaflet/dist/images/marker-icon.png";
import markerShadow from "leaflet/dist/images/marker-shadow.png";
import { useEffect, useRef, type RefObject } from "react";

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

// Replaces site.js's EcoMeal.map.render/destroy JS interop with a vanilla-Leaflet React hook —
// not react-leaflet, to keep the dependency surface the same as the Blazor version's. The map
// instance and its markers are torn down on unmount/re-render so repeated mounts (e.g. toggling
// Home's map view off and on) never leak a Leaflet instance.
export function useLeafletMap(containerRef: RefObject<HTMLElement | null>, markers: MapMarker[], onMarkerClick?: (id: string) => void) {
  const mapRef = useRef<L.Map | null>(null);
  const onMarkerClickRef = useRef(onMarkerClick);
  onMarkerClickRef.current = onMarkerClick;

  useEffect(() => {
    if (!containerRef.current) return;
    configureDefaultIcon();

    const map = L.map(containerRef.current);
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      attribution: "&copy; OpenStreetMap contributors",
      maxZoom: 19,
    }).addTo(map);
    mapRef.current = map;

    return () => {
      map.remove();
      mapRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [containerRef]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;

    const layerGroup = L.layerGroup().addTo(map);
    const bounds: [number, number][] = [];
    for (const marker of markers) {
      const leafletMarker = L.marker([marker.lat, marker.lng]).addTo(layerGroup);
      leafletMarker.bindPopup(`<strong>${escapeHtml(marker.name)}</strong>`);
      leafletMarker.on("click", () => onMarkerClickRef.current?.(marker.id));
      bounds.push([marker.lat, marker.lng]);
    }

    if (bounds.length > 0) map.fitBounds(bounds, { padding: [30, 30] });
    else map.setView(DEFAULT_CENTER, 12);

    return () => {
      layerGroup.remove();
    };
  }, [markers]);
}
