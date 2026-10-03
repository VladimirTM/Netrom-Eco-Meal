import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { businessesApi } from "../../api/clients/BusinessesApiClient";
import { favoritesApi } from "../../api/clients/FavoritesApiClient";
import { kitchenTipsApi } from "../../api/clients/KitchenTipsApiClient";
import { loyaltyApi } from "../../api/clients/LoyaltyApiClient";
import { ordersApi } from "../../api/clients/OrdersApiClient";
import { packagesApi } from "../../api/clients/PackagesApiClient";
import { reportsApi } from "../../api/clients/ReportsApiClient";
import { reviewsApi } from "../../api/clients/ReviewsApiClient";
import type { BusinessDto } from "../../api/models/Business";
import type { LoyaltyProgress } from "../../api/models/Loyalty";
import type { KitchenTipDto, PackageDto, ReviewContextDto, ReviewDto } from "../../api/models/Package";
import { standingOrdersApi } from "../../api/clients/StandingOrdersApiClient";
import type { StandingOrderDto } from "../../api/models/StandingOrder";
import ConfirmDialog from "../common/ConfirmDialog";
import LoadingSpinner from "../common/LoadingSpinner";
import NotFoundPanel from "../common/NotFoundPanel";
import PackageDetailModal from "../common/PackageDetailModal";
import ReportDialog from "../common/ReportDialog";
import StarRating from "../common/StarRating";
import { useAuth } from "../../context/AuthContext/auth-context";
import { useCart } from "../../context/CartContext/cart-context";
import { useTimeZone } from "../../context/TimeZoneContext/timezone-context";
import { useToast } from "../../context/ToastContext/toast-context";
import { useStockHub } from "../../hooks/useStockHub";
import { activeClosure, formatHoursRow, isOpenNow, todayDayName, WEEK_ORDER } from "../../utils/businessHoursStatus";
import { ALL_DIETARY_TAGS, isAllergen } from "../../utils/dietaryTags";
import { formatCurrency } from "../../utils/currency";
import { getInitial } from "../../utils/textHelpers";
import { formatDateOnly, formatLocalDate } from "../../utils/dates";
import { availableQuantity } from "../../utils/packageAvailability";
import { closingSoonLabel, formatPickupWindow } from "../../utils/packagePickup";

function heroStyle(imageUrl: string | null): React.CSSProperties {
  return imageUrl
    ? {
        backgroundColor: "var(--em-forest)",
        backgroundImage: `linear-gradient(180deg, rgba(11,31,19,0.15) 0%, rgba(11,31,19,0.85) 100%), url('${imageUrl}')`,
      }
    : { backgroundColor: "var(--em-forest)" };
}

function BusinessDetail() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuth();
  const cart = useCart();
  const { showToast } = useToast();
  const timeZone = useTimeZone();
  const isCustomer = user?.role === "Customer";

  const [loading, setLoading] = useState(true);
  const [business, setBusiness] = useState<BusinessDto | null>(null);
  const [packages, setPackages] = useState<PackageDto[]>([]);
  const [loadingPackages, setLoadingPackages] = useState(false);
  const [reservedByPackage, setReservedByPackage] = useState<Record<string, number>>({});
  const [reviews, setReviews] = useState<ReviewDto[]>([]);
  const [reviewContext, setReviewContext] = useState<ReviewContextDto | null>(null);
  const [kitchenTips, setKitchenTips] = useState<KitchenTipDto[]>([]);
  const [loyaltyProgress, setLoyaltyProgress] = useState<LoyaltyProgress | null>(null);
  const [isFavorite, setIsFavorite] = useState(false);
  const [selectedPackage, setSelectedPackage] = useState<PackageDto | null>(null);

  const [reviewRating, setReviewRating] = useState(0);
  const [reviewComment, setReviewComment] = useState("");
  const [reviewPackageId, setReviewPackageId] = useState("");
  const [submittingReview, setSubmittingReview] = useState(false);
  const [reviewError, setReviewError] = useState<string | null>(null);

  const [newTipText, setNewTipText] = useState("");
  const [submittingTip, setSubmittingTip] = useState(false);
  const [tipError, setTipError] = useState<string | null>(null);

  const [pendingAddPackage, setPendingAddPackage] = useState<PackageDto | null>(null);

  const [myStandingOrders, setMyStandingOrders] = useState<StandingOrderDto[]>([]);
  const [newStandingPackageTypeId, setNewStandingPackageTypeId] = useState("");
  const [newStandingDietaryTag, setNewStandingDietaryTag] = useState("");
  const [newStandingBudget, setNewStandingBudget] = useState(20);
  const [savingStandingOrder, setSavingStandingOrder] = useState(false);
  const [standingOrderError, setStandingOrderError] = useState<string | null>(null);

  const [reportOpen, setReportOpen] = useState(false);
  const [reportBusy, setReportBusy] = useState(false);
  const [reportTipId, setReportTipId] = useState<string | null>(null);

  const loadPackages = useCallback(async (businessId: string) => {
    const paged = await packagesApi.getPaged(1, 100, businessId);
    const now = Date.now();
    const live = paged.items
      .filter((p) => new Date(p.pickupEnd).getTime() > now && !p.isHidden)
      .sort((a, b) => new Date(a.pickupEnd).getTime() - new Date(b.pickupEnd).getTime());
    setPackages(live);
    const reserved = await ordersApi.getPendingReservedQuantities(live.map((p) => p.id));
    setReservedByPackage(reserved);
    // Keep an open detail modal pointed at the freshly-loaded package instance so its
    // "left"/"Sold out" state updates live too — closes itself if it dropped off the live list.
    setSelectedPackage((current) => (current ? (live.find((p) => p.id === current.id) ?? null) : current));
  }, []);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;

    (async () => {
      setLoading(true);
      let loaded: BusinessDto | null = null;
      try {
        loaded = await businessesApi.getById(id);
      } catch {
        loaded = null;
      }
      // A pending/rejected application or a hidden business isn't a public storefront page.
      const visible = loaded && loaded.status === "Approved" && !loaded.isHidden ? loaded : null;
      if (cancelled) return;
      setBusiness(visible);
      setLoading(false);

      if (!visible) return;

      const favoriteIds = isCustomer ? await favoritesApi.getMine() : [];
      if (cancelled) return;
      setIsFavorite(favoriteIds.includes(id));

      setLoadingPackages(true);
      await loadPackages(id);
      if (cancelled) return;
      setLoadingPackages(false);

      if (isCustomer) {
        const progress = await loyaltyApi.getMyProgress(id);
        if (!cancelled) setLoyaltyProgress(progress);
        const standingOrders = await standingOrdersApi.getMine();
        if (!cancelled) setMyStandingOrders(standingOrders);
      }

      const [businessReviews, context, tips] = await Promise.all([
        reviewsApi.getByBusiness(id),
        isCustomer ? reviewsApi.getContext(id) : Promise.resolve<ReviewContextDto | null>(null),
        kitchenTipsApi.getByBusiness(id),
      ]);
      if (cancelled) return;
      setReviews(businessReviews);
      setKitchenTips(tips);
      if (context) {
        setReviewContext(context);
        if (context.myReview) {
          setReviewRating(context.myReview.rating);
          setReviewComment(context.myReview.comment ?? "");
          setReviewPackageId(context.myReview.packageId ?? "");
        }
      }
    })();

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, isCustomer]);

  useStockHub(business ? id : undefined, () => {
    if (id) void loadPackages(id);
  });

  if (loading) return <LoadingSpinner />;
  if (!business) return <NotFoundPanel title="Business not found" message="This kitchen no longer exists — it may have closed or been removed." backHref="/" backLabel="Back to home" />;

  const averageRating = reviews.length === 0 ? 0 : reviews.reduce((s, r) => s + r.rating, 0) / reviews.length;
  const openNow = isOpenNow(business.hours, business.closures);
  const closure = activeClosure(business.closures);
  const packageReviews = (packageId: string) => reviews.filter((r) => r.packageId === packageId);
  const packageRatingAverage = (packageId: string) => {
    const list = packageReviews(packageId);
    return list.length === 0 ? 0 : list.reduce((s, r) => s + r.rating, 0) / list.length;
  };

  async function toggleFavorite() {
    if (!id) return;
    setIsFavorite(await favoritesApi.toggle(id));
  }

  const businessPackageTypes = Array.from(new Map(packages.map((p) => [p.packageTypeId, p.packageTypeName])).entries()).sort((a, b) =>
    a[1].localeCompare(b[1]),
  );
  const myStandingOrder = myStandingOrders.find((s) => s.businessId === id) ?? null;

  function addPackageToBasket(pkg: PackageDto) {
    if (!business) return;
    if (cart.wouldReplaceCart(business.id)) {
      setPendingAddPackage(pkg);
      return;
    }
    cart.addItem(business.id, business.name, pkg, 1);
    showToast(`Added ${pkg.name} to your basket.`, "success");
  }

  function confirmAddPackage() {
    const pkg = pendingAddPackage;
    setPendingAddPackage(null);
    if (!pkg || !business) return;
    cart.addItem(business.id, business.name, pkg, 1);
    showToast(`Added ${pkg.name} to your basket.`, "success");
  }

  async function createStandingOrderAsync() {
    if (!id) return;
    setSavingStandingOrder(true);
    setStandingOrderError(null);
    try {
      const created = await standingOrdersApi.create(id, newStandingPackageTypeId || null, newStandingDietaryTag || null, newStandingBudget);
      setMyStandingOrders((prev) => [...prev, created]);
    } catch (error) {
      setStandingOrderError(error instanceof Error ? error.message : "Couldn't save that standing order.");
    } finally {
      setSavingStandingOrder(false);
    }
  }

  async function toggleStandingOrderAsync() {
    if (!myStandingOrder) return;
    setSavingStandingOrder(true);
    await standingOrdersApi.update(myStandingOrder.id, myStandingOrder.maxWeeklySpend, !myStandingOrder.isActive);
    setMyStandingOrders((prev) => prev.map((s) => (s.id === myStandingOrder.id ? { ...s, isActive: !s.isActive } : s)));
    setSavingStandingOrder(false);
  }

  async function deleteStandingOrderAsync() {
    if (!myStandingOrder) return;
    setSavingStandingOrder(true);
    await standingOrdersApi.remove(myStandingOrder.id);
    setMyStandingOrders((prev) => prev.filter((s) => s.id !== myStandingOrder.id));
    setSavingStandingOrder(false);
  }

  async function submitReview() {
    if (!id || reviewRating < 1) return;
    setSubmittingReview(true);
    setReviewError(null);
    try {
      await reviewsApi.submit(id, reviewRating, reviewComment || null, reviewPackageId || null);
      const [businessReviews, context] = await Promise.all([reviewsApi.getByBusiness(id), reviewsApi.getContext(id)]);
      setReviews(businessReviews);
      setReviewContext(context);
    } catch (error) {
      setReviewError(error instanceof Error ? error.message : "Couldn't submit your review.");
    } finally {
      setSubmittingReview(false);
    }
  }

  async function submitTip() {
    if (!id || !newTipText.trim()) return;
    setSubmittingTip(true);
    setTipError(null);
    try {
      await kitchenTipsApi.submit(id, newTipText);
      setNewTipText("");
      setKitchenTips(await kitchenTipsApi.getByBusiness(id));
    } catch (error) {
      setTipError(error instanceof Error ? error.message : "Couldn't share your tip.");
    } finally {
      setSubmittingTip(false);
    }
  }

  async function submitReport(reason: string) {
    if (!reason.trim()) return;
    setReportBusy(true);
    await reportsApi.submit(reportTipId ? "KitchenTip" : "Business", reportTipId ?? business!.id, reason);
    setReportBusy(false);
    setReportOpen(false);
    setReportTipId(null);
  }

  return (
    <div className="biz-page">
      <Link to="/" className="biz-page-back">
        <i className="bi bi-arrow-left" /> Back to all kitchens
      </Link>

      <div className="biz-page-card">
        <div className="biz-modal-hero" style={heroStyle(business.imageUrl)}>
          <div className="biz-modal-hero-overlay">
            <span className="biz-modal-eyebrow">{business.businessTypeName}</span>
            <div className="d-flex align-items-start justify-content-between gap-2">
              <h1 className="biz-modal-title" id="biz-modal-title">
                {business.name}
              </h1>
              {isCustomer && (
                <div className="d-flex align-items-center gap-2">
                  <button type="button" className={`biz-modal-fav-btn ${isFavorite ? "biz-modal-fav-active" : ""}`} onClick={() => void toggleFavorite()}>
                    <i className={`bi ${isFavorite ? "bi-heart-fill" : "bi-heart"}`} /> {isFavorite ? "Favorited" : "Favorite"}
                  </button>
                  <button
                    type="button"
                    className="biz-modal-fav-btn"
                    title="Report this kitchen"
                    onClick={() => {
                      setReportTipId(null);
                      setReportOpen(true);
                    }}
                  >
                    <i className="bi bi-flag" />
                  </button>
                </div>
              )}
            </div>
            <div className="biz-modal-meta">
              {business.brandId && (
                <Link to={`/brands/${business.brandId}`} className="biz-modal-open-badge biz-modal-open-badge-open text-decoration-none">
                  <i className="bi bi-diagram-3" /> Part of {business.brandName}
                </Link>
              )}
              <span className="biz-modal-address">
                <i className="bi bi-geo-alt-fill" />
                {business.address}
              </span>
              {openNow !== null && (
                <span className={`biz-modal-open-badge ${openNow ? "biz-modal-open-badge-open" : "biz-modal-open-badge-closed"}`}>
                  <span className="biz-modal-open-dot" />
                  {openNow ? "Open now" : "Closed now"}
                </span>
              )}
              {reviews.length > 0 && <StarRating value={averageRating} showValue count={reviews.length} size="0.8rem" />}
            </div>
          </div>
        </div>

        <div className="biz-modal-body">
          <p className="biz-modal-desc">{business.description}</p>

          {isCustomer && loyaltyProgress && (
            <div className="biz-modal-inline-toast">
              <i className="bi bi-award-fill" />
              {loyaltyProgress.ordersUntilReward === 1
                ? `1 more completed order this month unlocks ${formatCurrency(loyaltyProgress.discountAmount)} off your next order here!`
                : `${loyaltyProgress.ordersUntilReward} more completed orders this month unlock ${formatCurrency(loyaltyProgress.discountAmount)} off your next order here!`}
            </div>
          )}

          {(business.hours.length > 0 || business.closures.length > 0) && (
            <>
              <div className="biz-modal-divider" />
              <section className="biz-modal-section">
                <h3 className="biz-modal-section-title">
                  <i className="bi bi-clock-history" /> Opening hours
                </h3>

                {closure && (
                  <div className="biz-modal-closure-notice">
                    <i className="bi bi-calendar-x" />
                    Closed for the holidays until {formatDateOnly(closure.endDate)}
                    {closure.reason ? ` — ${closure.reason}` : "."}
                  </div>
                )}

                {business.hours.length > 0 && (
                  <ul className="biz-hours-list">
                    {WEEK_ORDER.map((day) => {
                      const row = business.hours.find((h) => h.dayOfWeek === day);
                      const isToday = day === todayDayName();
                      return (
                        <li className={`biz-hours-row ${isToday ? "biz-hours-row-today" : ""}`} key={day}>
                          <span>{day}</span>
                          <span>{formatHoursRow(row)}</span>
                        </li>
                      );
                    })}
                  </ul>
                )}
              </section>
            </>
          )}

          <div className="biz-modal-divider" />

          <section className="biz-modal-section">
            <h3 className="biz-modal-section-title">
              <i className="bi bi-bag-check" /> What&apos;s available
              <span className="biz-live-badge" title="Stock updates automatically while you browse">
                <span className="biz-card-live-dot" />
                Live
              </span>
            </h3>

            {loadingPackages ? (
              <div className="d-flex justify-content-center py-4">
                <div className="spinner-border spinner-border-sm text-primary" role="status">
                  <span className="visually-hidden">Loading...</span>
                </div>
              </div>
            ) : packages.length === 0 ? (
              <p className="biz-modal-empty">Nothing live from this kitchen right now — check back later.</p>
            ) : (
              <div className="biz-pkg-list">
                {packages.map((pkg) => {
                  const available = availableQuantity(pkg.quantity, reservedByPackage[pkg.id] ?? 0, cart.inBasketQuantity(pkg.id));
                  const closingSoon = closingSoonLabel(pkg.pickupEnd);
                  return (
                    <div className="biz-pkg-row biz-pkg-row-clickable" key={pkg.id} onClick={() => setSelectedPackage(pkg)}>
                      <div className="biz-pkg-info">
                        <div className="biz-pkg-name">{pkg.name}</div>
                        <div className="biz-pkg-meta">
                          <span className="biz-pkg-type">{pkg.packageTypeName}</span>
                          <span>&middot;</span>
                          <span>
                            <i className="bi bi-clock" />
                            {formatPickupWindow(pkg.pickupStart, pkg.pickupEnd, timeZone)}
                          </span>
                          {closingSoon && (
                            <span className="biz-card-closed-badge">
                              <i className="bi bi-alarm-fill" />
                              {closingSoon}
                            </span>
                          )}
                        </div>
                        <p className="biz-pkg-desc">{pkg.description}</p>
                        {pkg.dietaryTags.length > 0 && (
                          <div className="diet-tag-row">
                            {pkg.dietaryTags.map((tag) => (
                              <span className={`diet-tag ${isAllergen(tag) ? "diet-tag-allergen" : ""}`} key={tag}>
                                {tag}
                              </span>
                            ))}
                          </div>
                        )}
                      </div>
                      <div className="biz-pkg-side">
                        <div className="biz-pkg-price">{formatCurrency(pkg.price)}</div>
                        <div className="biz-pkg-qty">{available} left</div>
                        {isCustomer && (
                          <button
                            type="button"
                            className="biz-pkg-add-btn"
                            disabled={available <= 0}
                            onClick={(e) => {
                              e.stopPropagation();
                              addPackageToBasket(pkg);
                            }}
                          >
                            <i className="bi bi-bag-plus" /> Add
                          </button>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </section>

          {isCustomer && (
            <>
              <div className="biz-modal-divider" />

              <section className="biz-modal-section">
                <h3 className="biz-modal-section-title">
                  <i className="bi bi-arrow-repeat" /> Standing order
                </h3>

                {myStandingOrder === null ? (
                  <>
                    <p className="biz-modal-review-hint">
                      Save your usual here — we&apos;ll add it to your basket and notify you the moment it&apos;s back, up to a weekly budget
                      you set.
                    </p>
                    <div className="row g-2 align-items-end">
                      <div className="col-sm-4">
                        <label className="form-label small">
                          Package type <span className="text-muted">(optional)</span>
                        </label>
                        <select className="form-select form-select-sm" value={newStandingPackageTypeId} onChange={(e) => setNewStandingPackageTypeId(e.target.value)}>
                          <option value="">Any type</option>
                          {businessPackageTypes.map(([typeId, typeName]) => (
                            <option value={typeId} key={typeId}>
                              {typeName}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div className="col-sm-4">
                        <label className="form-label small">
                          Dietary tag <span className="text-muted">(optional)</span>
                        </label>
                        <select className="form-select form-select-sm" value={newStandingDietaryTag} onChange={(e) => setNewStandingDietaryTag(e.target.value)}>
                          <option value="">Any</option>
                          {ALL_DIETARY_TAGS.map((tag) => (
                            <option value={tag} key={tag}>
                              {tag}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div className="col-sm-3">
                        <label className="form-label small">Weekly budget</label>
                        <div className="input-group input-group-sm">
                          <input
                            type="number"
                            min={1}
                            step={0.5}
                            className="form-control"
                            value={newStandingBudget}
                            onChange={(e) => setNewStandingBudget(Number(e.target.value))}
                          />
                          <span className="input-group-text">lei</span>
                        </div>
                      </div>
                      <div className="col-sm-1">
                        <button type="button" className="btn btn-primary btn-sm w-100" disabled={savingStandingOrder} onClick={() => void createStandingOrderAsync()}>
                          {savingStandingOrder ? <span className="spinner-border spinner-border-sm" role="status" /> : <span>Save</span>}
                        </button>
                      </div>
                    </div>
                    {standingOrderError && <div className="text-danger small mt-1">{standingOrderError}</div>}
                  </>
                ) : (
                  <div className="d-flex align-items-center justify-content-between flex-wrap gap-2">
                    <div className="small">
                      <strong>{myStandingOrder.packageTypeName ?? "Any package"}</strong>
                      {myStandingOrder.dietaryTag ? ` · ${myStandingOrder.dietaryTag}` : ""} — up to {formatCurrency(myStandingOrder.maxWeeklySpend)}/week
                      <span className={`badge ms-2 ${myStandingOrder.isActive ? "bg-success" : "bg-secondary"}`}>
                        {myStandingOrder.isActive ? "Active" : "Paused"}
                      </span>
                    </div>
                    <div className="d-flex gap-2">
                      <button type="button" className="btn btn-outline-secondary btn-sm" disabled={savingStandingOrder} onClick={() => void toggleStandingOrderAsync()}>
                        {myStandingOrder.isActive ? "Pause" : "Resume"}
                      </button>
                      <button type="button" className="btn btn-outline-danger btn-sm" disabled={savingStandingOrder} onClick={() => void deleteStandingOrderAsync()}>
                        Remove
                      </button>
                    </div>
                  </div>
                )}
              </section>
            </>
          )}

          <div className="biz-modal-divider" />

          <section className="biz-modal-section">
            <h3 className="biz-modal-section-title">
              <i className="bi bi-chat-square-text" /> Reviews
            </h3>

            {isCustomer &&
              (reviewContext?.canReview ? (
                <div className="biz-review-form">
                  <span className="biz-review-form-label">{reviewContext.myReview ? "Update your review" : "Leave a review"}</span>
                  <StarRating editable selectedValue={reviewRating} onSelectedValueChange={setReviewRating} size="1.4rem" />
                  {reviewContext.reviewablePackages.length > 0 && (
                    <select className="form-select form-select-sm biz-review-package-select" value={reviewPackageId} onChange={(e) => setReviewPackageId(e.target.value)}>
                      <option value="">Whole kitchen (no specific package)</option>
                      {reviewContext.reviewablePackages.map((p) => (
                        <option value={p.id} key={p.id}>
                          {p.name}
                        </option>
                      ))}
                    </select>
                  )}
                  <textarea
                    className="form-control biz-review-textarea"
                    rows={2}
                    maxLength={600}
                    placeholder="What did you think? (optional)"
                    value={reviewComment}
                    onChange={(e) => setReviewComment(e.target.value)}
                  />
                  {reviewError && <div className="alert alert-danger py-2 small mb-0">{reviewError}</div>}
                  <button type="button" className="btn btn-primary btn-sm align-self-start" disabled={reviewRating < 1 || submittingReview} onClick={() => void submitReview()}>
                    {submittingReview && <span className="spinner-border spinner-border-sm me-2" role="status" />}
                    {reviewContext.myReview ? "Update review" : "Submit review"}
                  </button>
                </div>
              ) : (
                <p className="biz-modal-review-hint">Order from {business.name} and come back to leave a review.</p>
              ))}

            {reviews.length === 0 ? (
              <p className="biz-modal-empty">No reviews yet — be the first to rescue a meal here.</p>
            ) : (
              <div className="biz-review-list">
                {reviews.map((review) => (
                  <div className="biz-review-card" key={review.id}>
                    <div className="biz-review-avatar">{getInitial(review.userName)}</div>
                    <div className="biz-review-content">
                      <div className="biz-review-top">
                        <span className="biz-review-name">{review.userName}</span>
                        <span className="biz-review-date">{formatLocalDate(review.createdAt, timeZone)}</span>
                      </div>
                      <div className="d-flex align-items-center gap-2 flex-wrap">
                        <StarRating value={review.rating} size="0.75rem" />
                      </div>
                      {review.comment && <p className="biz-review-comment">{review.comment}</p>}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>

          <div className="biz-modal-divider" />

          <section className="biz-modal-section">
            <h3 className="biz-modal-section-title">
              <i className="bi bi-lightbulb" /> Kitchen tips
            </h3>
            <p className="biz-modal-review-hint mt-0">Practical hints from other rescuers — parking, entrance, best time to show up.</p>

            {isCustomer && (
              <div className="biz-review-form">
                <textarea
                  className="form-control biz-review-textarea"
                  rows={2}
                  maxLength={200}
                  placeholder="e.g. Use the side door after 8pm"
                  value={newTipText}
                  onChange={(e) => setNewTipText(e.target.value)}
                />
                {tipError && <div className="alert alert-danger py-2 small mb-0">{tipError}</div>}
                <button type="button" className="btn btn-primary btn-sm align-self-start" disabled={!newTipText.trim() || submittingTip} onClick={() => void submitTip()}>
                  {submittingTip && <span className="spinner-border spinner-border-sm me-2" role="status" />}
                  Share tip
                </button>
              </div>
            )}

            {kitchenTips.length === 0 ? (
              <p className="biz-modal-empty">No tips yet — share the first one.</p>
            ) : (
              <div className="biz-review-list">
                {kitchenTips.map((tip) => (
                  <div className="biz-review-card" key={tip.id}>
                    <div className="biz-review-avatar">{getInitial(tip.userName)}</div>
                    <div className="biz-review-content">
                      <div className="biz-review-top">
                        <span className="biz-review-name">{tip.userName}</span>
                        <span className="biz-review-date">{formatLocalDate(tip.createdAt, timeZone)}</span>
                      </div>
                      <p className="biz-review-comment mb-0">{tip.tip}</p>
                    </div>
                    {user && (
                      <button
                        type="button"
                        className="btn btn-link btn-sm text-muted p-0"
                        title="Report this tip"
                        onClick={() => {
                          setReportTipId(tip.id);
                          setReportOpen(true);
                        }}
                      >
                        <i className="bi bi-flag" />
                      </button>
                    )}
                  </div>
                ))}
              </div>
            )}
          </section>
        </div>
      </div>

      <PackageDetailModal
        pkg={selectedPackage}
        availableQuantity={
          selectedPackage ? availableQuantity(selectedPackage.quantity, reservedByPackage[selectedPackage.id] ?? 0, cart.inBasketQuantity(selectedPackage.id)) : 0
        }
        ratingAverage={selectedPackage ? packageRatingAverage(selectedPackage.id) : 0}
        reviewCount={selectedPackage ? packageReviews(selectedPackage.id).length : 0}
        onClose={() => setSelectedPackage(null)}
      />

      <ReportDialog
        isOpen={reportOpen}
        targetLabel={reportTipId ? "this tip" : business.name}
        busy={reportBusy}
        onSubmit={submitReport}
        onCancel={() => {
          setReportOpen(false);
          setReportTipId(null);
        }}
      />

      <ConfirmDialog
        isOpen={pendingAddPackage !== null}
        title="Start a new basket?"
        message={`Your basket has items from ${cart.businessName}. Adding ${pendingAddPackage?.name ?? "this package"} will clear it and start a new one.`}
        confirmLabel="Start new basket"
        cancelLabel="Keep current basket"
        confirmClass="btn-primary"
        onConfirm={confirmAddPackage}
        onCancel={() => setPendingAddPackage(null)}
      />
    </div>
  );
}

export default BusinessDetail;
