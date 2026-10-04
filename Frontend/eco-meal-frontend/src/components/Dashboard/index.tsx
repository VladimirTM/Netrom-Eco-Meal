import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../../api/base/http";
import { businessesApi } from "../../api/clients/BusinessesApiClient";
import { ordersApi } from "../../api/clients/OrdersApiClient";
import { packagesApi } from "../../api/clients/PackagesApiClient";
import { usersApi } from "../../api/clients/UsersApiClient";
import type { OrderDto } from "../../api/models/Order";
import type { PackageDto } from "../../api/models/Package";
import { useAuth } from "../../context/AuthContext/auth-context";
import { useManagedBusiness } from "../../context/ManagedBusinessContext/managed-business-context";
import { useTimeZone } from "../../context/TimeZoneContext/timezone-context";
import ConfirmDialog from "../common/ConfirmDialog";
import {
  barHeightPx,
  buildCompletedQtyByPackage,
  buildDailyStats,
  computeAnalyticsStats,
  fourteenDaysAgoUtc,
  hourLabel,
  sellThroughRatePercent,
  type DailyStat,
} from "../../utils/dashboardAnalytics";

// Toggles its own label to "Copied!" for 1.5s — replaces the inline onclick handlers
// Dashboard.razor used for its two "Copy" buttons (navigator.clipboard isn't reachable from a
// Razor @onclick the same way, so this is a small self-contained React stand-in).
function CopyButton({ value }: { value: string }) {
  const [copied, setCopied] = useState(false);

  async function copy() {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      // Clipboard access can be denied (permissions policy, insecure context) — the input itself
      // is still selectable/copyable by hand, so this is a silent no-op, not an error state.
    }
  }

  return (
    <button type="button" className="btn btn-outline-secondary" onClick={copy}>
      {copied ? "Copied!" : "Copy"}
    </button>
  );
}

// Ports Dashboard.razor (/dashboard). Stats/analytics are scoped platform-wide for an Admin, or to
// whichever business is currently selected in the sidebar switcher for a BusinessManager.
function Dashboard() {
  const { user } = useAuth();
  const { myBusinesses, selectedBusinessId } = useManagedBusiness();
  const timeZone = useTimeZone();
  const isAdmin = user?.role === "Admin";
  const myBusinessId = isAdmin ? null : (selectedBusinessId ?? null);
  const myBusiness = useMemo(() => myBusinesses.find((b) => b.id === myBusinessId) ?? null, [myBusinesses, myBusinessId]);

  const [businessCount, setBusinessCount] = useState<number | null>(null);
  const [packageCount, setPackageCount] = useState<number | null>(null);
  const [orderCount, setOrderCount] = useState<number | null>(null);
  const [userCount, setUserCount] = useState<number | null>(null);
  const [ordersUnavailable, setOrdersUnavailable] = useState(false);

  const [dailyStats, setDailyStats] = useState<DailyStat[] | null>(null);
  const [analyticsPackages, setAnalyticsPackages] = useState<PackageDto[] | null>(null);
  const [completedQtyByPackage, setCompletedQtyByPackage] = useState<Map<string, number> | null>(null);

  const [impactPanelOpen, setImpactPanelOpen] = useState(false);
  const [webhookPanelOpen, setWebhookPanelOpen] = useState(false);

  // Seeds the webhook-key display state from the already-fetched ManagedBusinessContext business
  // (it carries hasWebhookApiKey/webhookApiKeyLastUsedAt already — no need for its own GetById
  // round trip) whenever the selected business changes, without a useEffect round trip — same
  // "adjusting state when a prop changes" pattern ManagedBusinessProvider uses for its own reset.
  const [seededForBusinessId, setSeededForBusinessId] = useState<string | null>(myBusinessId);
  const [hasApiKey, setHasApiKey] = useState(myBusiness?.hasWebhookApiKey ?? false);
  const [apiKeyLastUsedAt, setApiKeyLastUsedAt] = useState<string | null>(myBusiness?.webhookApiKeyLastUsedAt ?? null);
  const [generatedApiKey, setGeneratedApiKey] = useState<string | null>(null);
  const [apiKeyError, setApiKeyError] = useState<string | null>(null);
  const [generatingKey, setGeneratingKey] = useState(false);
  const [confirmingRevoke, setConfirmingRevoke] = useState(false);
  const [revokingKey, setRevokingKey] = useState(false);

  if (myBusinessId !== seededForBusinessId) {
    setSeededForBusinessId(myBusinessId);
    setHasApiKey(myBusiness?.hasWebhookApiKey ?? false);
    setApiKeyLastUsedAt(myBusiness?.webhookApiKeyLastUsedAt ?? null);
    setGeneratedApiKey(null);
    setApiKeyError(null);
  }

  const loadDashboard = useCallback(async () => {
    if (isAdmin) {
      const [businesses, allPackages, users] = await Promise.all([businessesApi.getAll(false), packagesApi.getAll(), usersApi.getPaged(1, 1)]);
      setBusinessCount(businesses.length);
      setPackageCount(allPackages.length);
      setUserCount(users.totalCount);
    } else {
      const allPackages = await packagesApi.getAll();
      setPackageCount(myBusinessId ? allPackages.filter((p) => p.businessId === myBusinessId).length : 0);
    }

    let orders: OrderDto[];
    try {
      orders = await ordersApi.getForManagement(myBusinessId ?? undefined);
    } catch (err) {
      if (err instanceof ApiError && (err.status === 401 || err.status === 403)) {
        setOrdersUnavailable(true);
        setOrderCount(null);
        setDailyStats(null);
        setAnalyticsPackages(null);
        setCompletedQtyByPackage(null);
        return;
      }
      throw err;
    }

    setOrdersUnavailable(false);
    setOrderCount(orders.length);

    const since = fourteenDaysAgoUtc();
    const sinceIso = since.toISOString();

    const [rangeOrders, analyticsPkgs] = await Promise.all([
      ordersApi.getInRange(sinceIso, null, myBusinessId ?? undefined),
      packagesApi.getForAnalytics(myBusinessId ?? undefined, sinceIso),
    ]);

    setDailyStats(buildDailyStats(rangeOrders, since));
    setAnalyticsPackages(analyticsPkgs);
    // Deliberately built from the full order history above (ordersApi.getForManagement), not
    // rangeOrders — see buildCompletedQtyByPackage's own comment.
    setCompletedQtyByPackage(buildCompletedQtyByPackage(orders));
  }, [isAdmin, myBusinessId]);

  useEffect(() => {
    // Synchronizes with the server whenever the signed-in role or selected business changes —
    // mirrors Dashboard.razor's OnInitializedAsync + ManagedBusinessContext.OnChange subscription.
    // oxlint-disable-next-line react/set-state-in-effect
    loadDashboard();
  }, [loadDashboard]);

  const analyticsStats = useMemo(() => {
    if (!analyticsPackages || !completedQtyByPackage) return null;
    return computeAnalyticsStats(analyticsPackages, completedQtyByPackage, timeZone);
  }, [analyticsPackages, completedQtyByPackage, timeZone]);

  const maxOrderCount = dailyStats ? Math.max(1, ...dailyStats.map((d) => d.orderCount)) : 1;
  const maxKgSaved = dailyStats ? Math.max(1, ...dailyStats.map((d) => d.kgSaved)) : 1;

  // VITE_API_URL always ends with /api (see .env.example) — both the impact-widget script tag and
  // the webhook endpoint/payload example live on the API host's own root, not the SPA's origin.
  const apiOrigin = (import.meta.env.VITE_API_URL as string).replace(/\/api\/?$/, "");
  const impactWidgetSnippet = `<script src="${apiOrigin}/js/impact-widget.js" data-business-id="${myBusinessId}"></script>`;
  const webhookEndpointUrl = `${apiOrigin}/api/webhooks/packages`;
  const webhookPayloadExample = JSON.stringify(
    {
      name: "Surprise Bag",
      description: "Today's leftover pastries",
      price: 9.99,
      quantity: 5,
      weightKg: 1.2,
      packageTypeId: "<see GET /api/package-types>",
      dietaryTags: ["Vegetarian"],
      pickupStart: `${todayIsoDate()}T17:00:00Z`,
      pickupEnd: `${todayIsoDate()}T20:00:00Z`,
    },
    null,
    2,
  );

  async function generateApiKey() {
    if (!myBusinessId) return;

    setApiKeyError(null);
    setGeneratingKey(true);
    try {
      const key = await businessesApi.generateApiKey(myBusinessId);
      setGeneratedApiKey(key);
      setHasApiKey(true);
      setApiKeyLastUsedAt(null);
    } catch (err) {
      setApiKeyError(err instanceof ApiError && err.status === 403 ? "You don't have permission to manage this business's webhook key." : "Couldn't generate a key right now. Please try again.");
    } finally {
      setGeneratingKey(false);
    }
  }

  async function revokeApiKey() {
    if (!myBusinessId) {
      setConfirmingRevoke(false);
      return;
    }

    setApiKeyError(null);
    setRevokingKey(true);
    try {
      await businessesApi.revokeApiKey(myBusinessId);
      setHasApiKey(false);
      setApiKeyLastUsedAt(null);
      setGeneratedApiKey(null);
    } catch (err) {
      setApiKeyError(err instanceof ApiError && err.status === 403 ? "You don't have permission to manage this business's webhook key." : "Couldn't revoke the key right now. Please try again.");
    } finally {
      setRevokingKey(false);
      setConfirmingRevoke(false);
    }
  }

  return (
    <div>
      <div className="mb-4">
        <h1 className="h3 fw-bold mb-1">Dashboard</h1>
        <p className="text-muted small mb-0">Welcome to the Eco Meal Admin Panel</p>
      </div>

      <div className="row g-3 mb-4">
        {isAdmin && (
          <div className="col-sm-6 col-xl-3">
            <div className="card border-0 shadow-sm h-100 card-accent-green">
              <div className="card-body d-flex align-items-center gap-3">
                <div className="rounded-3 p-3 bg-primary bg-opacity-10 text-primary">
                  <i className="bi bi-building fs-4" />
                </div>
                <div>
                  <div className="text-muted small">Businesses</div>
                  {businessCount === null ? (
                    <div className="spinner-border spinner-border-sm text-primary mt-1" role="status">
                      <span className="visually-hidden">Loading...</span>
                    </div>
                  ) : (
                    <div className="fw-bold fs-4">{businessCount}</div>
                  )}
                </div>
              </div>
            </div>
          </div>
        )}

        <div className="col-sm-6 col-xl-3">
          <div className="card border-0 shadow-sm h-100 card-accent-blue">
            <div className="card-body d-flex align-items-center gap-3">
              <div className="rounded-3 p-3 bg-sky-subtle text-sky">
                <i className="bi bi-box-seam fs-4" />
              </div>
              <div>
                <div className="text-muted small">{isAdmin ? "Packages" : "Your Packages"}</div>
                {packageCount === null ? (
                  <div className="spinner-border spinner-border-sm text-sky mt-1" role="status">
                    <span className="visually-hidden">Loading...</span>
                  </div>
                ) : (
                  <div className="fw-bold fs-4">{packageCount}</div>
                )}
              </div>
            </div>
          </div>
        </div>

        <div className="col-sm-6 col-xl-3">
          <div className="card border-0 shadow-sm h-100 card-accent-amber">
            <div className="card-body d-flex align-items-center gap-3">
              <div className="rounded-3 p-3 bg-warning bg-opacity-10 text-warning">
                <i className="bi bi-cart3 fs-4" />
              </div>
              <div>
                <div className="text-muted small">Orders</div>
                {ordersUnavailable ? (
                  <div className="fw-bold fs-6 text-muted">Not assigned</div>
                ) : orderCount === null ? (
                  <div className="spinner-border spinner-border-sm text-warning mt-1" role="status">
                    <span className="visually-hidden">Loading...</span>
                  </div>
                ) : (
                  <div className="fw-bold fs-4">{orderCount}</div>
                )}
              </div>
            </div>
          </div>
        </div>

        {isAdmin && (
          <div className="col-sm-6 col-xl-3">
            <div className="card border-0 shadow-sm h-100 card-accent-purple">
              <div className="card-body d-flex align-items-center gap-3">
                <div className="rounded-3 p-3 bg-purple-subtle text-purple">
                  <i className="bi bi-people fs-4" />
                </div>
                <div>
                  <div className="text-muted small">Users</div>
                  {userCount === null ? (
                    <div className="spinner-border spinner-border-sm text-purple mt-1" role="status">
                      <span className="visually-hidden">Loading...</span>
                    </div>
                  ) : (
                    <div className="fw-bold fs-4">{userCount}</div>
                  )}
                </div>
              </div>
            </div>
          </div>
        )}
      </div>

      <h2 className="h5 fw-bold mb-3">Analytics</h2>

      <div className="card border-0 shadow-sm mb-4">
        <div className="card-header bg-transparent border-0 pt-4 px-4 pb-0">
          <h6 className="fw-semibold mb-0">Last 14 days</h6>
        </div>
        <div className="card-body px-4 pb-4">
          {ordersUnavailable ? (
            <p className="text-muted small mb-0">You don't manage a business yet, so there's no trend to show.</p>
          ) : dailyStats === null ? (
            <div className="d-flex justify-content-center py-4">
              <div className="spinner-border spinner-border-sm text-primary" role="status">
                <span className="visually-hidden">Loading...</span>
              </div>
            </div>
          ) : (
            <>
              <div className="dash-trend-row">
                <span className="dash-trend-label">Orders</span>
                <div className="dash-trend-chart">
                  {dailyStats.map((day, i) => {
                    const isToday = i === dailyStats.length - 1;
                    const label = formatDayLabel(day.date, isToday);
                    return (
                      <div className={`dash-trend-bar-wrap ${isToday ? "dash-trend-bar-wrap-today" : ""}`} tabIndex={0} aria-label={`${label}: ${day.orderCount} order${day.orderCount === 1 ? "" : "s"}`} key={day.date}>
                        <span className="dash-trend-tooltip">
                          <span className="dash-trend-tooltip-date">{isToday ? "Today" : label}</span>
                          <span className="dash-trend-tooltip-value">
                            {day.orderCount} order{day.orderCount === 1 ? "" : "s"}
                          </span>
                        </span>
                        <div className="dash-trend-bar dash-trend-bar-orders" style={{ height: `${barHeightPx(day.orderCount, maxOrderCount)}px` }} />
                      </div>
                    );
                  })}
                </div>
              </div>
              <div className="dash-trend-row">
                <span className="dash-trend-label">Kg saved</span>
                <div className="dash-trend-chart">
                  {dailyStats.map((day, i) => {
                    const isToday = i === dailyStats.length - 1;
                    const label = formatDayLabel(day.date, isToday);
                    const kgLabel = `${day.kgSaved.toFixed(1).replace(/\.0$/, "")} kg saved`;
                    return (
                      <div className={`dash-trend-bar-wrap ${isToday ? "dash-trend-bar-wrap-today" : ""}`} tabIndex={0} aria-label={`${label}: ${kgLabel}`} key={day.date}>
                        <span className="dash-trend-tooltip">
                          <span className="dash-trend-tooltip-date">{isToday ? "Today" : label}</span>
                          <span className="dash-trend-tooltip-value">{kgLabel}</span>
                        </span>
                        <div className="dash-trend-bar dash-trend-bar-kg" style={{ height: `${barHeightPx(day.kgSaved, maxKgSaved)}px` }} />
                      </div>
                    );
                  })}
                </div>
              </div>
              <div className="dash-trend-row dash-trend-axis-row">
                <span className="dash-trend-label" />
                <div className="dash-trend-chart">
                  {dailyStats.map((day, i) => {
                    const isToday = i === dailyStats.length - 1;
                    return (
                      <div className="dash-trend-axis-tick" key={day.date}>
                        {isToday ? <span className="dash-trend-axis-today">Today</span> : i % 2 === 0 ? <span>{formatDayLabel(day.date, false)}</span> : null}
                      </div>
                    );
                  })}
                </div>
              </div>
            </>
          )}
        </div>
      </div>

      <div className="card border-0 shadow-sm mb-4">
        <div className="card-header bg-transparent border-0 pt-4 px-4 pb-0">
          <h6 className="fw-semibold mb-0">Business Analytics</h6>
          <p className="text-muted small mb-0">Packages with a pickup window in the last 14 days.</p>
        </div>
        <div className="card-body px-4 pb-4">
          {ordersUnavailable ? (
            <p className="text-muted small mb-0">You don't manage a business yet, so there's no analytics to show.</p>
          ) : analyticsStats === null ? (
            <div className="d-flex justify-content-center py-4">
              <div className="spinner-border spinner-border-sm text-primary" role="status">
                <span className="visually-hidden">Loading...</span>
              </div>
            </div>
          ) : (
            <div className="row g-4">
              <div className="col-md-5">
                <div className="text-muted small mb-1">Sell-through rate</div>
                {analyticsStats.soldQty + analyticsStats.unsoldQty === 0 ? (
                  <p className="text-muted small mb-0">No packages with a closed pickup window yet in this period.</p>
                ) : (
                  <>
                    <div className="d-flex align-items-baseline gap-2 mb-2">
                      <span className="fw-bold fs-2">{sellThroughRatePercent(analyticsStats.sellThroughRate)}%</span>
                      <span className="text-muted small">
                        {analyticsStats.soldQty} of {analyticsStats.soldQty + analyticsStats.unsoldQty} listed picked up
                      </span>
                    </div>
                    <div className="progress" style={{ height: 8 }}>
                      <div
                        className="progress-bar bg-success"
                        role="progressbar"
                        style={{ width: `${sellThroughRatePercent(analyticsStats.sellThroughRate)}%` }}
                        aria-valuenow={sellThroughRatePercent(analyticsStats.sellThroughRate)}
                        aria-valuemin={0}
                        aria-valuemax={100}
                      />
                    </div>
                    <div className="text-muted small mt-2">
                      {analyticsStats.closedPackageCount} package{analyticsStats.closedPackageCount === 1 ? "" : "s"} with a closed pickup window.
                    </div>
                  </>
                )}
              </div>
              <div className="col-md-7">
                <div className="text-muted small mb-2" title="Every completed pickup in this period, by hour — unlike sell-through rate to the left, this isn't limited to packages whose pickup window has already closed.">
                  Busiest pickup hours <span className="text-muted">(completed pickups)</span>
                </div>
                <div className="dash-trend-chart" style={{ height: 56 }}>
                  {analyticsStats.hourlyPickupQty.map((qty, hour) => (
                    <div className="dash-trend-bar-wrap" tabIndex={0} aria-label={`${hourLabel(hour)}: ${qty} pickup${qty === 1 ? "" : "s"}`} key={hour}>
                      <span className="dash-trend-tooltip">
                        <span className="dash-trend-tooltip-date">{hourLabel(hour)}</span>
                        <span className="dash-trend-tooltip-value">
                          {qty} pickup{qty === 1 ? "" : "s"}
                        </span>
                      </span>
                      <div className="dash-trend-bar dash-trend-bar-hours" style={{ height: `${barHeightPx(qty, analyticsStats.maxHourlyQty)}px` }} />
                    </div>
                  ))}
                </div>
                <div className="dash-trend-chart dash-trend-axis-row">
                  {Array.from({ length: 24 }, (_, h) => (
                    <div className="dash-trend-axis-tick" key={h}>
                      {h % 3 === 0 ? `${h}h` : ""}
                    </div>
                  ))}
                </div>
              </div>
            </div>
          )}
        </div>
      </div>

      {myBusinessId !== null && (
        <>
          <h2 className="h5 fw-bold mb-3">Business tools</h2>

          <div className="card border-0 shadow-sm mb-3">
            <button
              type="button"
              className="card-header btn w-100 d-flex justify-content-between align-items-center text-start p-4 border-0 bg-transparent"
              aria-expanded={impactPanelOpen}
              onClick={() => setImpactPanelOpen((open) => !open)}
            >
              <span>
                <span className="fw-semibold d-block">Share your impact</span>
                <span className="text-muted small">A public, read-only widget for your own website — pulls your live numbers straight from Eco Meal, no login needed on your end.</span>
              </span>
              <i className={`bi ${impactPanelOpen ? "bi-chevron-up" : "bi-chevron-down"} ms-3 flex-shrink-0`} />
            </button>
            {impactPanelOpen && (
              <div className="card-body px-4 pb-4 pt-0">
                <div className="input-group">
                  <input type="text" className="form-control font-monospace small" readOnly value={impactWidgetSnippet} onClick={(e) => (e.target as HTMLInputElement).select()} />
                  <CopyButton value={impactWidgetSnippet} />
                </div>
                <p className="text-muted small mt-2 mb-0">Paste this anywhere in your site's HTML — it renders your kitchen's total kg saved plus a CO2e/water equivalency.</p>
              </div>
            )}
          </div>

          <div className="card border-0 shadow-sm mb-4">
            <button
              type="button"
              className="card-header btn w-100 d-flex justify-content-between align-items-center text-start p-4 border-0 bg-transparent"
              aria-expanded={webhookPanelOpen}
              onClick={() => setWebhookPanelOpen((open) => !open)}
            >
              <span>
                <span className="fw-semibold d-block">POS / inventory webhook</span>
                <span className="text-muted small">Let your own POS or inventory system create a live package the moment it's marked as end-of-day surplus — no need to type it into Add Package by hand.</span>
              </span>
              <i className={`bi ${webhookPanelOpen ? "bi-chevron-up" : "bi-chevron-down"} ms-3 flex-shrink-0`} />
            </button>
            {webhookPanelOpen && (
              <div className="card-body px-4 pb-4 pt-0">
                {apiKeyError && <div className="alert alert-danger py-2 small">{apiKeyError}</div>}
                {generatedApiKey && (
                  <div className="alert alert-warning py-2 small">
                    <strong>Copy this key now — it won't be shown again.</strong>
                    <div className="input-group mt-2">
                      <input type="text" className="form-control font-monospace small" readOnly value={generatedApiKey} onClick={(e) => (e.target as HTMLInputElement).select()} />
                      <CopyButton value={generatedApiKey} />
                    </div>
                  </div>
                )}

                <div className="d-flex align-items-center gap-2 mb-3">
                  <button type="button" className="btn btn-outline-primary btn-sm" disabled={generatingKey} onClick={generateApiKey}>
                    {generatingKey && <span className="spinner-border spinner-border-sm me-1" role="status" />}
                    {hasApiKey ? "Regenerate key" : "Generate API key"}
                  </button>
                  {hasApiKey && (
                    <>
                      <button type="button" className="btn btn-outline-danger btn-sm" onClick={() => setConfirmingRevoke(true)}>
                        Revoke key
                      </button>
                      <span className="text-muted small">{apiKeyLastUsedAt ? `Last used ${new Date(apiKeyLastUsedAt).toISOString().slice(0, 16).replace("T", " ")} UTC` : "Never used yet"}</span>
                    </>
                  )}
                </div>

                <p className="text-muted small mb-1">
                  Endpoint: <code>POST {webhookEndpointUrl}</code>
                </p>
                <p className="text-muted small mb-2">
                  Header: <code>X-Api-Key: &lt;your key&gt;</code>
                </p>
                <pre className="bg-body-secondary p-2 small rounded">
                  <code>{webhookPayloadExample}</code>
                </pre>
                <p className="text-muted small mb-0">
                  Valid <code>packageTypeId</code> values come from <code>GET /api/package-types</code> — no auth needed.
                </p>
              </div>
            )}
          </div>

          <ConfirmDialog
            isOpen={confirmingRevoke}
            title="Revoke this webhook key?"
            message="Your POS/inventory system will immediately stop being able to create packages until you generate a new one."
            confirmLabel="Revoke key"
            busy={revokingKey}
            onConfirm={revokeApiKey}
            onCancel={() => setConfirmingRevoke(false)}
          />
        </>
      )}

      <div className="card border-0 shadow-sm">
        <div className="card-header bg-transparent border-0 pt-4 px-4 pb-0">
          <h6 className="fw-semibold mb-0">Quick Actions</h6>
        </div>
        <div className="card-body px-4 pb-4">
          <div className="row g-2">
            <div className="col-sm-6 col-lg-3">
              <Link to="/businesses" className="btn btn-outline-secondary w-100 d-flex align-items-center gap-2">
                <i className="bi bi-building" /> All Businesses
              </Link>
            </div>
            {isAdmin && (
              <div className="col-sm-6 col-lg-3">
                <Link to="/businesses/create" className="btn btn-outline-primary w-100 d-flex align-items-center gap-2">
                  <i className="bi bi-plus-lg" /> Add Business
                </Link>
              </div>
            )}
            <div className="col-sm-6 col-lg-3">
              <Link to="/packages" className="btn btn-outline-secondary w-100 d-flex align-items-center gap-2">
                <i className="bi bi-box-seam" /> All Packages
              </Link>
            </div>
            {/* Dashboard itself is already Admin/BusinessManager-only (route-gated), same set of
                roles Dashboard.razor's AuthorizeView wrapped these two in — no extra role check needed. */}
            <div className="col-sm-6 col-lg-3">
              <Link to="/packages/create" className="btn btn-outline-primary w-100 d-flex align-items-center gap-2">
                <i className="bi bi-plus-lg" /> Add Package
              </Link>
            </div>
            <div className="col-sm-6 col-lg-3">
              <Link to="/orders/manage" className="btn btn-outline-secondary w-100 d-flex align-items-center gap-2">
                <i className="bi bi-cart3" /> Manage Orders
              </Link>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

// "yyyy-MM-dd" -> "MMM d", using the fixed calendar date parts directly (not `new Date(iso)`,
// which would reinterpret it in the browser's own local time zone and could roll it a day either
// way) — mirrors Dashboard.razor's `day.Date.ToString("MMM d")` over the UTC-bucketed DateOnly.
// Hoisted out of the component body so computing "today" isn't a direct `new Date()` call in the
// render path (oxlint's react/purity rule flags that) — same shape as PackageTemplates' own
// top-level toLocalTimeLabel helper.
function todayIsoDate(): string {
  return new Date().toISOString().slice(0, 10);
}

const MONTH_NAMES = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
function formatDayLabel(dateKey: string, isToday: boolean): string {
  if (isToday) return "Today";
  const [, month, day] = dateKey.split("-").map(Number);
  return `${MONTH_NAMES[month - 1]} ${day}`;
}

export default Dashboard;
