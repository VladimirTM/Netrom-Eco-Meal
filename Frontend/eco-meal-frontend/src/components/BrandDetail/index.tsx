import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { brandsApi } from "../../api/clients/BrandsApiClient";
import type { BrandDetailResponseDto } from "../../api/models/Business";
import LoadingSpinner from "../common/LoadingSpinner";
import NotFoundPanel from "../common/NotFoundPanel";
import StarRating from "../common/StarRating";
import { useAuth } from "../../context/AuthContext/auth-context";
import { useGeolocation } from "../../hooks/useGeolocation";
import { isOpenNow } from "../../utils/businessHoursStatus";
import { distanceKm } from "../../utils/geoDistance";

function BrandDetail() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuth();
  const isCustomer = user?.role === "Customer";
  const geo = useGeolocation();

  const [detail, setDetail] = useState<BrandDetailResponseDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [isFavorite, setIsFavorite] = useState(false);
  const [position, setPosition] = useState<{ lat: number; lng: number } | null>(null);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;

    (async () => {
      setLoading(true);
      let loaded: BrandDetailResponseDto | null = null;
      try {
        loaded = await brandsApi.getDetail(id);
      } catch {
        loaded = null;
      }
      if (cancelled) return;
      setDetail(loaded);
      setLoading(false);

      if (loaded && isCustomer) {
        const favoriteIds = await brandsApi.getMyFavoriteIds();
        if (!cancelled) setIsFavorite(favoriteIds.includes(id));
      }

      // Same belt-and-suspenders geolocation timeout as the Trip Planner fix — never blocks the page.
      if (loaded && loaded.locations.length > 0) {
        const located = await geo.locate();
        if (!cancelled && located) setPosition(located);
      }
    })();

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, isCustomer]);

  const orderedLocations = useMemo(() => {
    if (!detail) return [];
    const views = detail.locations.map((location) => ({
      location,
      openNow: isOpenNow(location.hours, location.closures),
      distanceKm:
        position && location.latitude !== null && location.longitude !== null
          ? distanceKm(position.lat, position.lng, location.latitude, location.longitude)
          : null,
    }));
    return views.sort((a, b) => {
      if ((a.openNow === true) !== (b.openNow === true)) return a.openNow === true ? -1 : 1;
      const da = a.distanceKm ?? Number.MAX_VALUE;
      const db = b.distanceKm ?? Number.MAX_VALUE;
      if (da !== db) return da - db;
      return a.location.name.localeCompare(b.location.name);
    });
  }, [detail, position]);

  const nearestOpenLocation = position ? (orderedLocations.find((v) => v.openNow === true) ?? null) : null;

  async function toggleFavorite() {
    if (!id) return;
    setIsFavorite(await brandsApi.toggleFavorite(id));
  }

  if (loading) return <LoadingSpinner />;
  if (!detail) return <NotFoundPanel title="Brand not found" message="This brand no longer exists — it may have been removed." backHref="/brands" backLabel="Back to brands" />;

  return (
    <div className="container py-4" style={{ maxWidth: 900 }}>
      <Link to="/brands" className="d-inline-block mb-3 text-decoration-none">
        <i className="bi bi-arrow-left" /> Back to brands
      </Link>

      <div className="card border-0 shadow-sm mb-4">
        <div className="card-body p-4">
          <div className="d-flex align-items-start justify-content-between gap-2 flex-wrap">
            <div>
              <h1 className="h3 fw-bold mb-1">{detail.brand.name}</h1>
              {detail.brand.description && <p className="text-muted mb-2">{detail.brand.description}</p>}
              {detail.ratingCount > 0 && <StarRating value={detail.averageRating ?? 0} showValue count={detail.ratingCount} size="0.9rem" />}
              <div className="text-muted small mt-1">
                <i className="bi bi-geo-alt me-1" />
                {detail.locations.length} location{detail.locations.length === 1 ? "" : "s"}
              </div>
            </div>
            {isCustomer && (
              <button type="button" className={`btn ${isFavorite ? "btn-primary" : "btn-outline-primary"}`} onClick={() => void toggleFavorite()}>
                <i className={`bi ${isFavorite ? "bi-heart-fill" : "bi-heart"}`} /> {isFavorite ? "Favorited" : "Favorite this brand"}
              </button>
            )}
          </div>
        </div>
      </div>

      {nearestOpenLocation && (
        <Link to={`/businesses/${nearestOpenLocation.location.id}`} className="text-decoration-none">
          <div className="alert alert-success d-flex align-items-center gap-2 mb-4">
            <i className="bi bi-signpost-2-fill fs-5" />
            <div>
              <div className="fw-semibold">Nearest location open now: {nearestOpenLocation.location.name}</div>
              <div className="small">
                {nearestOpenLocation.distanceKm !== null ? `${nearestOpenLocation.distanceKm.toFixed(1)} km away — ` : ""}Order now &rarr;
              </div>
            </div>
          </div>
        </Link>
      )}

      <h2 className="h6 fw-bold mb-3">Locations</h2>
      {detail.locations.length === 0 ? (
        <p className="text-muted">No live locations right now.</p>
      ) : (
        <div className="list-group">
          {orderedLocations.map(({ location, openNow, distanceKm: distance }) => (
            <Link to={`/businesses/${location.id}`} className="list-group-item list-group-item-action d-flex justify-content-between align-items-center" key={location.id}>
              <div>
                <div className="fw-semibold">{location.name}</div>
                <div className="text-muted small">
                  <i className="bi bi-geo-alt-fill me-1" />
                  {location.address}
                </div>
              </div>
              <div className="text-end">
                {openNow !== null && <span className={`badge ${openNow ? "text-bg-success" : "text-bg-secondary"}`}>{openNow ? "Open now" : "Closed now"}</span>}
                {distance !== null && <div className="text-muted small mt-1">{distance.toFixed(1)} km</div>}
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}

export default BrandDetail;
