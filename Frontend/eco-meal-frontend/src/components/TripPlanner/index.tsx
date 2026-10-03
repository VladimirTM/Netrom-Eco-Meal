import { useEffect, useRef, useState } from "react";
import { businessesApi } from "../../api/clients/BusinessesApiClient";
import { ordersApi } from "../../api/clients/OrdersApiClient";
import type { OrderDto } from "../../api/models/Order";
import { useGeolocation } from "../../hooks/useGeolocation";
import { useLeafletMap } from "../../hooks/useLeafletMap";
import { orderStatusCssSuffix, orderStatusLabel } from "../../utils/orderStatus";
import { planRoute } from "../../utils/tripRoute";

interface Stop {
  businessId: string;
  businessName: string;
  address: string;
  lat: number;
  lng: number;
  orders: OrderDto[];
}

// Ports TripPlanner.razor (/trip-planner) — one stop per business (an active order can't span
// businesses), ordered by a nearest-neighbor walk (utils/tripRoute.ts, ported from
// NetromEcoMeal.Models.TripPlanner.PlanRoute).
function TripPlanner() {
  const [orders, setOrders] = useState<OrderDto[] | null>(null);
  const [stops, setStops] = useState<Stop[]>([]);
  const [locating, setLocating] = useState(true);
  const [locationDenied, setLocationDenied] = useState(false);
  const { locate } = useGeolocation();
  const mapContainerRef = useRef<HTMLDivElement>(null);

  useLeafletMap(
    mapContainerRef,
    stops.map((s, i) => ({ id: s.businessId, name: `${i + 1}. ${s.businessName}`, lat: s.lat, lng: s.lng })),
  );

  async function buildStops(startLat: number | null, startLng: number | null, loadedOrders: OrderDto[]) {
    const active = loadedOrders.filter((o) => o.status === "Pending" || o.status === "Confirmed");
    const distinctBusinessIds = Array.from(new Set(active.map((o) => o.businessId)));
    const businesses = await Promise.all(
      distinctBusinessIds.map((businessId) => businessesApi.getById(businessId).catch(() => null)),
    );

    const rawStops: Stop[] = [];
    for (const business of businesses) {
      if (!business || business.latitude === null || business.longitude === null) continue;
      rawStops.push({
        businessId: business.id,
        businessName: business.name,
        address: business.address,
        lat: business.latitude,
        lng: business.longitude,
        orders: active.filter((o) => o.businessId === business.id).sort((a, b) => a.orderNumber - b.orderNumber),
      });
    }

    const routeOrder = planRoute(rawStops.map((s) => ({ id: s.businessId, lat: s.lat, lng: s.lng })), startLat, startLng);
    setStops(routeOrder.map((id) => rawStops.find((s) => s.businessId === id)!));
  }

  useEffect(() => {
    (async () => {
      const loaded = await ordersApi.getMine();
      setOrders(loaded);
      await buildStops(null, null, loaded);

      const activeCount = loaded.filter((o) => o.status === "Pending" || o.status === "Confirmed").length;
      if (activeCount > 1) {
        const position = await locate();
        setLocating(false);
        if (position) {
          await buildStops(position.lat, position.lng, loaded);
        } else {
          setLocationDenied(true);
        }
      } else {
        setLocating(false);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="container py-4">
      <div className="mb-4">
        <h1 className="h3 fw-bold mb-1">
          <i className="bi bi-signpost-2 me-2" />
          Trip planner
        </h1>
        <p className="text-muted mb-0">A suggested pickup order across every rescue you&apos;ve got waiting right now — nearest stop first.</p>
      </div>

      {orders === null ? (
        <div className="d-flex justify-content-center py-5">
          <div className="spinner-border text-primary" role="status">
            <span className="visually-hidden">Loading...</span>
          </div>
        </div>
      ) : stops.length === 0 ? (
        <div className="text-center py-5">
          <div className="em-empty-icon">
            <i className="bi bi-signpost-2" />
          </div>
          <p className="text-muted mb-1">No active orders to plan a route for yet.</p>
          <span className="text-muted small d-block mb-3">Place an order (or two!) and they&apos;ll show up here once they&apos;re pending or confirmed.</span>
          <a href="/" className="btn btn-primary px-4">
            Browse packages
          </a>
        </div>
      ) : (
        <>
          {stops.length === 1 ? (
            <div className="alert alert-info">Just one active order right now — plan a route once you&apos;ve got more than one on the go.</div>
          ) : locating ? (
            <div className="alert alert-secondary small">
              <span className="spinner-border spinner-border-sm me-2" />
              Trying to start the route from your current location…
            </div>
          ) : locationDenied ? (
            <div className="alert alert-secondary small">Couldn&apos;t get your location, so the route below starts from the first stop instead.</div>
          ) : null}

          <div className="row g-3">
            <div className="col-lg-5">
              <ol className="list-group list-group-numbered">
                {stops.map((stop) => (
                  <li className="list-group-item d-flex justify-content-between align-items-start" key={stop.businessId}>
                    <div className="ms-2 me-auto">
                      <div className="fw-semibold">{stop.businessName}</div>
                      <div className="text-muted small">
                        <i className="bi bi-geo-alt" /> {stop.address}
                      </div>
                      {stop.orders.map((order) => (
                        <div className="small" key={order.id}>
                          Order #{String(order.orderNumber).padStart(3, "0")}{" "}
                          <span className={`order-status-badge order-status-${orderStatusCssSuffix(order.status)}`}>{orderStatusLabel(order.status)}</span>
                        </div>
                      ))}
                    </div>
                  </li>
                ))}
              </ol>
            </div>
            <div className="col-lg-7">
              <div className="card border-0 shadow-sm home-map-wrap">
                <div id="trip-map" className="home-map" ref={mapContainerRef} />
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

export default TripPlanner;
