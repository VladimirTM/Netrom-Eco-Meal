import { useState } from "react";
import { reportsApi } from "../../../api/clients/ReportsApiClient";
import type { PackageDto } from "../../../api/models/Package";
import { useAuth } from "../../../context/AuthContext/auth-context";
import { useCart } from "../../../context/CartContext/cart-context";
import { useTimeZone } from "../../../context/TimeZoneContext/timezone-context";
import { useToast } from "../../../context/ToastContext/toast-context";
import { formatCurrency } from "../../../utils/currency";
import { isAllergen } from "../../../utils/dietaryTags";
import { formatPickupWindow } from "../../../utils/packagePickup";
import ConfirmDialog from "../ConfirmDialog";
import ReportDialog from "../ReportDialog";
import StarRating from "../StarRating";

interface PackageDetailModalProps {
  pkg: PackageDto | null;
  availableQuantity: number;
  ratingAverage: number;
  reviewCount: number;
  onClose: () => void;
}

// Ports PackageDetailModal.razor, plus the "add to basket" control.
function PackageDetailModal({ pkg, availableQuantity, ratingAverage, reviewCount, onClose }: PackageDetailModalProps) {
  const { user } = useAuth();
  const cart = useCart();
  const { showToast } = useToast();
  const timeZone = useTimeZone();
  const [reportOpen, setReportOpen] = useState(false);
  const [reportBusy, setReportBusy] = useState(false);
  const [reportSent, setReportSent] = useState(false);
  const [addQuantity, setAddQuantity] = useState(1);
  const [pendingReplace, setPendingReplace] = useState(false);

  // React's documented "adjusting state when a prop changes" pattern — a fresh package selection
  // resets any leftover report state from the previous one, without a useEffect roundtrip.
  const [prevPackageId, setPrevPackageId] = useState<string | null>(pkg?.id ?? null);
  if ((pkg?.id ?? null) !== prevPackageId) {
    setPrevPackageId(pkg?.id ?? null);
    if (!pkg) {
      setReportOpen(false);
      setReportSent(false);
    }
    setAddQuantity(1);
  }

  if (!pkg) return null;

  const heroStyle = pkg.imageUrl ? { backgroundImage: `url('${pkg.imageUrl}')` } : undefined;
  const clampedAddQuantity = Math.max(1, Math.min(addQuantity, Math.max(1, availableQuantity)));

  async function submitReport(reason: string) {
    if (!pkg || !reason.trim()) return;
    setReportBusy(true);
    await reportsApi.submit("Package", pkg.id, reason);
    setReportBusy(false);
    setReportOpen(false);
    setReportSent(true);
  }

  function addToBasket() {
    if (!pkg || availableQuantity <= 0) return;
    if (cart.wouldReplaceCart(pkg.businessId)) {
      setPendingReplace(true);
      return;
    }
    cart.addItem(pkg.businessId, pkg.businessName, pkg, clampedAddQuantity);
    showToast(`Added ${pkg.name} to your basket.`, "success");
  }

  function confirmReplace() {
    setPendingReplace(false);
    if (!pkg) return;
    cart.addItem(pkg.businessId, pkg.businessName, pkg, clampedAddQuantity);
    showToast(`Added ${pkg.name} to your basket.`, "success");
  }

  return (
    <>
      <div className="pkg-modal-backdrop" onClick={onClose} />
      <div className="pkg-modal" role="dialog" aria-modal="true" aria-labelledby="pkg-modal-title">
        <button type="button" className="biz-modal-close" onClick={onClose} aria-label="Close">
          <i className="bi bi-x-lg" />
        </button>

        {pkg.imageUrl && <div className="pkg-modal-hero" style={heroStyle} />}

        <div className="pkg-modal-body">
          <span className="biz-modal-eyebrow">{pkg.packageTypeName}</span>
          <h2 className="pkg-modal-title" id="pkg-modal-title">
            {pkg.name}
          </h2>
          <div className="pkg-modal-price">{formatCurrency(pkg.price)}</div>

          {reviewCount > 0 && <StarRating value={ratingAverage} showValue count={reviewCount} size="0.8rem" />}

          <p className="biz-modal-desc">{pkg.description}</p>

          {pkg.dietaryTags.length > 0 && (
            <div className="diet-tag-row mb-3">
              {pkg.dietaryTags.map((tag) => (
                <span className={`diet-tag ${isAllergen(tag) ? "diet-tag-allergen" : ""}`} key={tag}>
                  {tag}
                </span>
              ))}
            </div>
          )}

          <div className="pkg-modal-facts">
            <div className="pkg-modal-fact">
              <i className="bi bi-shop" />
              <div>
                <div className="pkg-modal-fact-label">From</div>
                <div className="pkg-modal-fact-value">{pkg.businessName}</div>
              </div>
            </div>
            <div className="pkg-modal-fact">
              <i className="bi bi-clock" />
              <div>
                <div className="pkg-modal-fact-label">Pickup window</div>
                <div className="pkg-modal-fact-value">{formatPickupWindow(pkg.pickupStart, pkg.pickupEnd, timeZone)}</div>
              </div>
            </div>
            <div className="pkg-modal-fact">
              <i className="bi bi-bag-check" />
              <div>
                <div className="pkg-modal-fact-label">Available</div>
                <div className="pkg-modal-fact-value">{availableQuantity} left</div>
              </div>
            </div>
          </div>

          {user?.role === "Customer" && (
            <>
              {availableQuantity > 0 ? (
                <div className="d-flex align-items-center gap-2 mb-3">
                  <div className="cart-line-controls">
                    <button type="button" className="cart-step-btn" disabled={clampedAddQuantity <= 1} onClick={() => setAddQuantity((q) => q - 1)}>
                      <i className="bi bi-dash" />
                    </button>
                    <span className="cart-line-qty">{clampedAddQuantity}</span>
                    <button
                      type="button"
                      className="cart-step-btn"
                      disabled={clampedAddQuantity >= availableQuantity}
                      onClick={() => setAddQuantity((q) => q + 1)}
                    >
                      <i className="bi bi-plus" />
                    </button>
                  </div>
                  <button type="button" className="btn btn-primary flex-grow-1" onClick={addToBasket}>
                    <i className="bi bi-bag-plus me-2" />
                    Add to basket
                  </button>
                </div>
              ) : (
                <p className="text-muted small text-center mb-3">Sold out for now.</p>
              )}
            </>
          )}

          {user?.role === "Customer" && (
            <button type="button" className="btn btn-link btn-sm w-100 text-muted" onClick={() => setReportOpen(true)}>
              <i className="bi bi-flag" /> Report this package
            </button>
          )}

          {reportSent && (
            <p className="text-success small text-center mb-0">
              <i className="bi bi-check-circle-fill" /> Thanks — an admin will take a look.
            </p>
          )}
        </div>
      </div>

      <ReportDialog
        isOpen={reportOpen}
        targetLabel={pkg.name}
        busy={reportBusy}
        onSubmit={submitReport}
        onCancel={() => setReportOpen(false)}
      />

      <ConfirmDialog
        isOpen={pendingReplace}
        title="Start a new basket?"
        message={`Your basket has items from ${cart.businessName}. Adding ${pkg.name} will clear it and start a new one.`}
        confirmLabel="Start new basket"
        cancelLabel="Keep current basket"
        confirmClass="btn-primary"
        onConfirm={confirmReplace}
        onCancel={() => setPendingReplace(false)}
      />
    </>
  );
}

export default PackageDetailModal;
