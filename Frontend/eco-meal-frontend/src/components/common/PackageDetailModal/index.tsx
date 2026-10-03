import { useState } from "react";
import { reportsApi } from "../../../api/clients/ReportsApiClient";
import type { PackageDto } from "../../../api/models/Package";
import { useAuth } from "../../../context/AuthContext/auth-context";
import { useTimeZone } from "../../../context/TimeZoneContext/timezone-context";
import { formatCurrency } from "../../../utils/currency";
import { isAllergen } from "../../../utils/dietaryTags";
import { formatPickupWindow } from "../../../utils/packagePickup";
import ReportDialog from "../ReportDialog";
import StarRating from "../StarRating";

interface PackageDetailModalProps {
  pkg: PackageDto | null;
  availableQuantity: number;
  ratingAverage: number;
  reviewCount: number;
  onClose: () => void;
}

// Ports PackageDetailModal.razor. "Add to basket" is Phase 6 scope (it needs CartContext, which
// doesn't exist yet) — this shows the same facts/report flow Phase 5 owns and leaves the
// available-quantity line to double as the add-to-cart button's eventual home.
function PackageDetailModal({ pkg, availableQuantity, ratingAverage, reviewCount, onClose }: PackageDetailModalProps) {
  const { user } = useAuth();
  const timeZone = useTimeZone();
  const [reportOpen, setReportOpen] = useState(false);
  const [reportBusy, setReportBusy] = useState(false);
  const [reportSent, setReportSent] = useState(false);

  // React's documented "adjusting state when a prop changes" pattern — a fresh package selection
  // resets any leftover report state from the previous one, without a useEffect roundtrip.
  const [prevPackageId, setPrevPackageId] = useState<string | null>(pkg?.id ?? null);
  if ((pkg?.id ?? null) !== prevPackageId) {
    setPrevPackageId(pkg?.id ?? null);
    if (!pkg) {
      setReportOpen(false);
      setReportSent(false);
    }
  }

  if (!pkg) return null;

  const heroStyle = pkg.imageUrl ? { backgroundImage: `url('${pkg.imageUrl}')` } : undefined;

  async function submitReport(reason: string) {
    if (!pkg || !reason.trim()) return;
    setReportBusy(true);
    await reportsApi.submit("Package", pkg.id, reason);
    setReportBusy(false);
    setReportOpen(false);
    setReportSent(true);
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
    </>
  );
}

export default PackageDetailModal;
