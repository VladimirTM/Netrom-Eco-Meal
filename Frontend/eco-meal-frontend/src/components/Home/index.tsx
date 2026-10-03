import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { aiApi } from "../../api/clients/AiApiClient";
import { businessTypesApi } from "../../api/clients/BusinessTypesApiClient";
import { businessesApi } from "../../api/clients/BusinessesApiClient";
import { favoritesApi } from "../../api/clients/FavoritesApiClient";
import { ordersApi } from "../../api/clients/OrdersApiClient";
import { packagesApi } from "../../api/clients/PackagesApiClient";
import { reviewsApi } from "../../api/clients/ReviewsApiClient";
import type { SearchIntent } from "../../api/models/Ai";
import type { BusinessDto } from "../../api/models/Business";
import type { BusinessTypeDto } from "../../api/models/Lookup";
import type { PackageDto, ReviewDto } from "../../api/models/Package";
import type { PaginatedList } from "../../api/models/Pagination";
import { ApiError } from "../../api/base/http";
import { BusinessCardSkeletons } from "../common/Skeleton";
import EmptyState from "../common/EmptyState";
import Pagination from "../common/Pagination";
import StarRating from "../common/StarRating";
import { useAuth } from "../../context/AuthContext/auth-context";
import { useGeolocation } from "../../hooks/useGeolocation";
import { useDebouncedValue } from "../../hooks/useDebouncedValue";
import { useLeafletMap, type MapMarker } from "../../hooks/useLeafletMap";
import { ALLERGEN_TAGS, ALL_DIETARY_TAGS } from "../../utils/dietaryTags";
import { BusinessSortOptions } from "../../utils/businessSortOptions";
import { formatCurrency } from "../../utils/currency";
import { distanceKm, formatDistance } from "../../utils/geoDistance";
import { isOpenNow } from "../../utils/businessHoursStatus";
import { availableQuantity } from "../../utils/packageAvailability";

const PAGE_SIZE = 9;

function mediaStyle(imageUrl: string | null): React.CSSProperties {
  return imageUrl
    ? { backgroundColor: "var(--em-forest)", backgroundImage: `url('${imageUrl}')` }
    : { backgroundColor: "var(--em-forest)" };
}

function Home() {
  const navigate = useNavigate();
  const { isAuthenticated, user } = useAuth();
  const isCustomer = isAuthenticated && user?.role === "Customer";
  const geo = useGeolocation();

  const [businessTypes, setBusinessTypes] = useState<BusinessTypeDto[]>([]);
  const [livePackages, setLivePackages] = useState<PackageDto[] | null>(null);
  const [reservedByPackage, setReservedByPackage] = useState<Record<string, number>>({});
  const [totalKgSaved, setTotalKgSaved] = useState<number | null>(null);
  const [favoriteBusinessIds, setFavoriteBusinessIds] = useState<Set<string>>(new Set());

  const [paged, setPaged] = useState<PaginatedList<BusinessDto> | null>(null);
  const [reviewsByBusiness, setReviewsByBusiness] = useState<Record<string, ReviewDto[]>>({});
  const [loadingPage, setLoadingPage] = useState(true);

  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedValue(search, 300);
  const [businessTypeFilter, setBusinessTypeFilter] = useState("");
  const [sortBy, setSortBy] = useState<string>(BusinessSortOptions.Name);
  const [dietaryTagFilter, setDietaryTagFilter] = useState("");
  const [maxPriceFilter, setMaxPriceFilter] = useState<number | undefined>(undefined);
  const [favoritesOnly, setFavoritesOnly] = useState(false);
  const [pageIndex, setPageIndex] = useState(1);
  const [filtersOpen, setFiltersOpen] = useState(false);
  // Bumped after an un-favorite while favoritesOnly is on, so the current page re-fetches even
  // though none of the effect's other dependencies changed (mirrors Home.razor's own
  // ToggleFavoriteAsync → ReloadAsync() call).
  const [reloadToken, setReloadToken] = useState(0);

  const [customerLat, setCustomerLat] = useState<number | undefined>(undefined);
  const [customerLng, setCustomerLng] = useState<number | undefined>(undefined);

  const [aiQuery, setAiQuery] = useState("");
  const [parsingAiSearch, setParsingAiSearch] = useState(false);
  const [aiSearchError, setAiSearchError] = useState<string | null>(null);
  const [lastSearchIntent, setLastSearchIntent] = useState<SearchIntent | null>(null);

  const [showMap, setShowMap] = useState(false);
  const [mapBusinesses, setMapBusinesses] = useState<BusinessDto[] | null>(null);
  const mapContainerRef = useRef<HTMLDivElement | null>(null);

  const hasActiveFilters =
    search !== "" ||
    businessTypeFilter !== "" ||
    dietaryTagFilter !== "" ||
    maxPriceFilter !== undefined ||
    sortBy !== BusinessSortOptions.Name ||
    favoritesOnly;

  const activeFilterCount =
    (businessTypeFilter !== "" ? 1 : 0) +
    (dietaryTagFilter !== "" ? 1 : 0) +
    (maxPriceFilter !== undefined ? 1 : 0) +
    (sortBy !== BusinessSortOptions.Name ? 1 : 0) +
    (favoritesOnly ? 1 : 0);

  // Hero stats + per-card "N live"/"from X" figures — loaded once, independent of the paged list's
  // own filters/sort (same split Home.razor makes between _livePackages and _paged).
  useEffect(() => {
    let cancelled = false;
    (async () => {
      const [types, packages, publicBusinesses, kgSaved, favorites] = await Promise.all([
        businessTypesApi.getAll(),
        packagesApi.getAll(),
        businessesApi.getAll(true),
        ordersApi.getTotalKgSaved(),
        isAuthenticated && user?.role === "Customer" ? favoritesApi.getMine() : Promise.resolve<string[]>([]),
      ]);
      if (cancelled) return;

      const publicBusinessIds = new Set(publicBusinesses.map((b) => b.id));
      const now = Date.now();
      const live = packages.filter((p) => new Date(p.pickupEnd).getTime() > now && !p.isHidden && publicBusinessIds.has(p.businessId));
      const reservedForLive = await ordersApi.getPendingReservedQuantities(live.map((p) => p.id));
      if (cancelled) return;

      setBusinessTypes(types);
      setLivePackages(live);
      setReservedByPackage(reservedForLive);
      setTotalKgSaved(kgSaved);
      setFavoriteBusinessIds(new Set(favorites));
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated, user?.role]);

  // The paged, filtered/sorted business grid — reloads whenever a filter, the debounced search
  // text, or the page index changes. No delay for filter dropdowns (FilterChangedAsync's own
  // behavior); only the search box itself is debounced, via debouncedSearch above.
  useEffect(() => {
    let cancelled = false;
    setLoadingPage(true);
    (async () => {
      const page = await businessesApi.getPaged({
        pageIndex,
        pageSize: PAGE_SIZE,
        search: debouncedSearch || undefined,
        businessTypeId: businessTypeFilter || undefined,
        sortBy,
        favoritesOnly,
        customerLat,
        customerLng,
        publicOnly: true,
        dietaryTag: dietaryTagFilter || undefined,
        maxPrice: maxPriceFilter,
      });
      if (cancelled) return;
      setPaged(page);

      const businessIds = page.items.map((b) => b.id);
      const reviews = businessIds.length > 0 ? await reviewsApi.getByBusinesses(businessIds) : [];
      if (cancelled) return;

      const grouped: Record<string, ReviewDto[]> = {};
      for (const review of reviews) {
        (grouped[review.businessId] ??= []).push(review);
      }
      setReviewsByBusiness(grouped);
      setLoadingPage(false);
    })();
    return () => {
      cancelled = true;
    };
  }, [pageIndex, debouncedSearch, businessTypeFilter, sortBy, dietaryTagFilter, maxPriceFilter, favoritesOnly, customerLat, customerLng, reloadToken]);

  // Any filter change other than typing in the search box resets to page 1, same as
  // Home.razor's FilterChangedAsync.
  function applyFilterChange(apply: () => void) {
    apply();
    setPageIndex(1);
  }

  async function toggleNearMe() {
    if (sortBy === BusinessSortOptions.Distance) {
      applyFilterChange(() => setSortBy(BusinessSortOptions.Name));
      return;
    }

    const position = await geo.locate();
    if (!position) return;
    setCustomerLat(position.lat);
    setCustomerLng(position.lng);
    applyFilterChange(() => setSortBy(BusinessSortOptions.Distance));
  }

  async function runAiSearch() {
    if (!aiQuery.trim() || parsingAiSearch) return;

    setAiSearchError(null);
    setParsingAiSearch(true);
    try {
      const intent = await aiApi.parseSearchIntent(aiQuery, lastSearchIntent);
      await applyIntent(intent);
    } catch (error) {
      setAiSearchError(error instanceof ApiError ? error.message : "The AI search assistant isn't available right now.");
    } finally {
      setParsingAiSearch(false);
    }
  }

  async function applyIntent(intent: SearchIntent) {
    setLastSearchIntent(intent);
    setSearch(intent.keywords ?? "");
    setDietaryTagFilter(intent.dietaryTag ?? "");
    setMaxPriceFilter(intent.maxPrice ?? undefined);

    let lat = customerLat;
    if (intent.nearMe && lat === undefined) {
      const position = await geo.locate();
      if (position) {
        lat = position.lat;
        setCustomerLat(position.lat);
        setCustomerLng(position.lng);
      }
    }

    // Only one sort slot exists — closingSoon wins when both are requested, since it needs no
    // permission prompt and is the app's core food-waste signal.
    const nextSort = intent.closingSoon ? BusinessSortOptions.ClosingSoon : intent.nearMe && lat !== undefined ? BusinessSortOptions.Distance : BusinessSortOptions.Name;
    applyFilterChange(() => setSortBy(nextSort));
  }

  function clearFilters() {
    setSearch("");
    setBusinessTypeFilter("");
    setDietaryTagFilter("");
    setMaxPriceFilter(undefined);
    setAiQuery("");
    setAiSearchError(null);
    setLastSearchIntent(null);
    applyFilterChange(() => {
      setSortBy(BusinessSortOptions.Name);
      setFavoritesOnly(false);
    });
  }

  async function toggleFavorite(businessId: string) {
    const isFavorite = await favoritesApi.toggle(businessId);
    setFavoriteBusinessIds((prev) => {
      const next = new Set(prev);
      if (isFavorite) next.add(businessId);
      else next.delete(businessId);
      return next;
    });
    if (favoritesOnly) setReloadToken((t) => t + 1);
  }

  async function toggleMap() {
    const next = !showMap;
    setShowMap(next);
    if (next && mapBusinesses === null) {
      const all = await businessesApi.getAll(true);
      setMapBusinesses(all.filter((b) => b.latitude !== null && b.longitude !== null));
    }
  }

  const mapMarkers = useMemo<MapMarker[]>(
    () => (mapBusinesses ?? []).map((b) => ({ id: b.id, name: b.name, lat: b.latitude!, lng: b.longitude! })),
    [mapBusinesses],
  );
  useLeafletMap(showMap ? mapContainerRef : { current: null }, mapMarkers, (id) => navigate(`/businesses/${id}`));

  function livePackagesFor(businessId: string): PackageDto[] {
    return (livePackages ?? []).filter((p) => p.businessId === businessId);
  }

  function reviewsFor(businessId: string): ReviewDto[] {
    return reviewsByBusiness[businessId] ?? [];
  }

  function availableSum(): number {
    if (!livePackages) return 0;
    return livePackages.reduce((sum, p) => sum + availableQuantity(p.quantity, reservedByPackage[p.id] ?? 0), 0);
  }

  return (
    <>
      <section className="home-hero">
        <div className="home-hero-inner">
          <span className="home-hero-eyebrow">
            <i className="bi bi-geo-alt-fill" /> Near you, live now
          </span>
          <h1 className="home-hero-title">
            Today&apos;s surplus,
            <br />
            <span className="home-hero-highlight">rescued</span> before it&apos;s binned.
          </h1>
          <p className="home-hero-sub">
            Local restaurants, bakeries and cafés list the food they&apos;d otherwise throw out at closing time — at a
            fraction of the price. Pick a kitchen to see what&apos;s left and book your pickup.
          </p>

          <div className="home-stats">
            <div className="home-stat">
              <span className="home-stat-value">{livePackages === null ? "—" : livePackages.length.toLocaleString()}</span>
              <span className="home-stat-label">packages live</span>
            </div>
            <div className="home-stat">
              <span className="home-stat-value">{livePackages === null ? "—" : availableSum().toLocaleString()}</span>
              <span className="home-stat-label">portions to save</span>
            </div>
            <div className="home-stat">
              <span className="home-stat-value">
                {livePackages === null ? "—" : new Set(livePackages.map((p) => p.businessId)).size.toLocaleString()}
              </span>
              <span className="home-stat-label">kitchens on board</span>
            </div>
            <div className="home-stat">
              <span className="home-stat-value">{totalKgSaved === null ? "—" : totalKgSaved.toFixed(1).replace(/\.0$/, "")}</span>
              <span className="home-stat-label">kg of food saved</span>
            </div>
          </div>
        </div>
      </section>

      <section className="home-browse">
        {paged && (
          <>
            <div className="home-ai-search">
              <div className="input-group">
                <span className="input-group-text border-end-0">
                  <i className="bi bi-stars text-muted" />
                </span>
                <input
                  type="text"
                  className="form-control border-start-0 ps-0 home-ai-search-input"
                  placeholder='Or describe what you want — "vegan dinner under 30 lei, closing soon"…'
                  value={aiQuery}
                  onChange={(e) => setAiQuery(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") void runAiSearch();
                  }}
                />
                <button
                  type="button"
                  className="btn btn-primary home-ai-search-btn"
                  disabled={parsingAiSearch || !aiQuery.trim()}
                  onClick={() => void runAiSearch()}
                >
                  {parsingAiSearch ? <span className="spinner-border spinner-border-sm" role="status" /> : <i className="bi bi-send" />} Ask AI
                </button>
              </div>
              {aiSearchError && <div className="text-danger small mt-1">{aiSearchError}</div>}
            </div>

            <div className="home-toolbar">
              <div className="input-group home-search">
                <span className="input-group-text border-end-0">
                  <i className="bi bi-search text-muted" />
                </span>
                <input
                  type="text"
                  className="form-control border-start-0 ps-0"
                  placeholder="Search kitchens or packages…"
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPageIndex(1);
                  }}
                />
              </div>

              <button
                type="button"
                className={`home-fav-toggle ${activeFilterCount > 0 ? "home-fav-toggle-active" : ""}`}
                onClick={() => setFiltersOpen(true)}
              >
                <i className="bi bi-sliders" /> Filters
                {activeFilterCount > 0 && <span className="home-filters-count">{activeFilterCount}</span>}
              </button>

              <button type="button" className={`home-fav-toggle ${showMap ? "home-fav-toggle-active" : ""}`} onClick={() => void toggleMap()}>
                <i className={`bi ${showMap ? "bi-list-ul" : "bi-map"}`} /> {showMap ? "List view" : "Map view"}
              </button>
            </div>

            {filtersOpen && (
              <>
                <div className="em-popover-backdrop" onClick={() => setFiltersOpen(false)} />
                <div className="em-popover home-filters-panel">
                  <div className="em-popover-header">
                    <span>Filters</span>
                    <button type="button" className="em-popover-close" aria-label="Close filters" onClick={() => setFiltersOpen(false)}>
                      <i className="bi bi-x-lg" />
                    </button>
                  </div>
                  <div className="em-popover-body">
                    <div className="em-popover-field">
                      <label className="form-label small text-muted mb-1">Kitchen type</label>
                      <select
                        className="form-select"
                        value={businessTypeFilter}
                        onChange={(e) => applyFilterChange(() => setBusinessTypeFilter(e.target.value))}
                      >
                        <option value="">All kitchen types</option>
                        {businessTypes.map((bt) => (
                          <option value={bt.id} key={bt.id}>
                            {bt.name}
                          </option>
                        ))}
                      </select>
                    </div>

                    <div className="em-popover-field">
                      <label className="form-label small text-muted mb-1">Sort by</label>
                      <select className="form-select" value={sortBy} onChange={(e) => applyFilterChange(() => setSortBy(e.target.value))}>
                        <option value={BusinessSortOptions.Name}>Name (A–Z)</option>
                        <option value={BusinessSortOptions.ClosingSoon}>Closing soon</option>
                        {customerLat !== undefined && <option value={BusinessSortOptions.Distance}>Nearest</option>}
                      </select>
                    </div>

                    <div className="em-popover-field">
                      <label className="form-label small text-muted mb-1">Diet / allergen</label>
                      <select
                        className="form-select"
                        value={dietaryTagFilter}
                        onChange={(e) => applyFilterChange(() => setDietaryTagFilter(e.target.value))}
                      >
                        <option value="">Any diet/allergen</option>
                        <optgroup label="Dietary preference">
                          {ALL_DIETARY_TAGS.filter((tag) => !(ALLERGEN_TAGS as string[]).includes(tag)).map((tag) => (
                            <option value={tag} key={tag}>
                              {tag}
                            </option>
                          ))}
                        </optgroup>
                        <optgroup label="Contains (allergen warning)">
                          {ALLERGEN_TAGS.map((tag) => (
                            <option value={tag} key={tag}>
                              {tag}
                            </option>
                          ))}
                        </optgroup>
                      </select>
                    </div>

                    <div className="em-popover-toggle-row">
                      <button
                        type="button"
                        className={`home-fav-toggle ${sortBy === BusinessSortOptions.Distance ? "home-fav-toggle-active" : ""}`}
                        onClick={() => void toggleNearMe()}
                        disabled={geo.locating}
                      >
                        {geo.locating ? <span className="spinner-border spinner-border-sm" role="status" /> : <i className="bi bi-geo-alt-fill" />} Near me
                      </button>

                      {isCustomer && (
                        <button
                          type="button"
                          className={`home-fav-toggle ${favoritesOnly ? "home-fav-toggle-active" : ""}`}
                          onClick={() => applyFilterChange(() => setFavoritesOnly((v) => !v))}
                        >
                          <i className={`bi ${favoritesOnly ? "bi-heart-fill" : "bi-heart"}`} /> Favorites
                        </button>
                      )}
                    </div>

                    {geo.error && <div className="text-danger small">{geo.error}</div>}

                    {maxPriceFilter !== undefined && (
                      <div className="home-chip-row mb-0">
                        <button type="button" className="home-chip home-chip-active" onClick={() => applyFilterChange(() => setMaxPriceFilter(undefined))}>
                          Under {formatCurrency(maxPriceFilter)} <i className="bi bi-x-lg" />
                        </button>
                      </div>
                    )}
                  </div>
                  <div className="em-popover-footer">
                    <button type="button" className="btn btn-outline-secondary btn-sm" disabled={!hasActiveFilters} onClick={clearFilters}>
                      <i className="bi bi-x-lg me-1" />
                      Clear all
                    </button>
                    <button type="button" className="btn btn-primary btn-sm" onClick={() => setFiltersOpen(false)}>
                      Done
                    </button>
                  </div>
                </div>
              </>
            )}

            <div className="home-results-row">
              <span className="text-muted small">
                {paged.totalCount} kitchen{paged.totalCount === 1 ? "" : "s"} found
              </span>
            </div>
          </>
        )}

        {showMap ? (
          <div className="home-map-wrap card border-0 shadow-sm mb-4">
            {mapBusinesses === null ? (
              <div className="d-flex justify-content-center py-5">
                <div className="spinner-border text-primary" role="status">
                  <span className="visually-hidden">Loading...</span>
                </div>
              </div>
            ) : mapBusinesses.length === 0 ? (
              <EmptyState icon="bi-map" message="No kitchens have a saved location yet." />
            ) : (
              <div id="home-map" className="home-map" ref={mapContainerRef} />
            )}
          </div>
        ) : paged === null ? (
          <BusinessCardSkeletons />
        ) : paged.items.length === 0 ? (
          <div className="home-empty">
            <EmptyState
              icon="bi-shop"
              message={paged.totalCount === 0 && !hasActiveFilters ? "No kitchens have joined yet — check back soon." : "Nothing matches your filters yet."}
            />
          </div>
        ) : (
          <>
            <div className="biz-grid" aria-busy={loadingPage}>
              {paged.items.map((business) => {
                const live = livePackagesFor(business.id);
                const reviews = reviewsFor(business.id);
                const closedNow = isOpenNow(business.hours, business.closures) === false;
                const distance =
                  customerLat !== undefined && customerLng !== undefined && business.latitude !== null && business.longitude !== null
                    ? formatDistance(distanceKm(customerLat, customerLng, business.latitude, business.longitude))
                    : null;
                const avgRating = reviews.length > 0 ? reviews.reduce((s, r) => s + r.rating, 0) / reviews.length : 0;

                return (
                  <button type="button" className="biz-card" key={business.id} onClick={() => navigate(`/businesses/${business.id}`)}>
                    <div className="biz-card-media" style={mediaStyle(business.imageUrl)}>
                      <i className="bi bi-shop biz-card-media-placeholder-icon" aria-hidden="true" />
                      {live.length > 0 && (
                        <span className="biz-card-live-badge">
                          <span className="biz-card-live-dot" />
                          {live.length} live
                        </span>
                      )}
                      {reviews.length > 0 && (
                        <span className="biz-card-rating-badge">
                          <StarRating value={avgRating} size="0.65rem" />
                          {avgRating.toFixed(1)}
                        </span>
                      )}
                      {isCustomer && (
                        <button
                          type="button"
                          className={`biz-card-fav-btn ${favoriteBusinessIds.has(business.id) ? "biz-card-fav-active" : ""}`}
                          onClick={(e) => {
                            e.stopPropagation();
                            void toggleFavorite(business.id);
                          }}
                          aria-label={favoriteBusinessIds.has(business.id) ? "Remove from favorites" : "Add to favorites"}
                        >
                          <i className={`bi ${favoriteBusinessIds.has(business.id) ? "bi-heart-fill" : "bi-heart"}`} />
                        </button>
                      )}
                    </div>
                    <div className="biz-card-body">
                      <div className="d-flex align-items-center justify-content-between">
                        <span className="biz-card-type">{business.businessTypeName}</span>
                        {closedNow && <span className="biz-card-closed-badge">Closed now</span>}
                      </div>
                      <h2 className="biz-card-name">{business.name}</h2>
                      <div className="biz-card-address">
                        <i className="bi bi-geo-alt-fill" />
                        {business.address}
                      </div>
                      {distance && (
                        <div className="biz-card-distance">
                          <i className="bi bi-signpost-2-fill" />
                          {distance}
                        </div>
                      )}
                      <p className="biz-card-desc">{business.description}</p>
                      <div className="biz-card-footer">
                        {live.length > 0 ? (
                          <span className="biz-card-from">from {formatCurrency(Math.min(...live.map((p) => p.price)))}</span>
                        ) : (
                          <span className="biz-card-from biz-card-from-muted">Nothing live now</span>
                        )}
                        <span className="biz-card-cta">
                          View kitchen <i className="bi bi-arrow-right" />
                        </span>
                      </div>
                    </div>
                  </button>
                );
              })}
            </div>

            <div className="home-pagination">
              <Pagination currentPage={paged.pageIndex} totalPages={paged.totalPages} onPageChange={setPageIndex} />
            </div>
          </>
        )}
      </section>
    </>
  );
}

export default Home;
