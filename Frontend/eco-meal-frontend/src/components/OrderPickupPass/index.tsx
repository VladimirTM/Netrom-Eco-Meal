import QRCode from "qrcode";
import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ordersApi } from "../../api/clients/OrdersApiClient";
import { ApiError } from "../../api/base/http";
import type { OrderDto, PickupPassDto } from "../../api/models/Order";
import ForbiddenPanel from "../common/ForbiddenPanel";
import NotFoundPanel from "../common/NotFoundPanel";
import { useTimeZone } from "../../context/TimeZoneContext/timezone-context";
import { formatCurrency } from "../../utils/currency";
import { formatOrderPickupWindow } from "../../utils/packagePickup";

const MIN_PASSES = 1;
const MAX_PASSES = 6;

function statusExplanation(status: string): string {
  switch (status) {
    case "Pending":
      return "Your pickup pass appears once the business confirms this order.";
    case "Completed":
      return "This order has already been picked up.";
    case "Cancelled":
      return "This order was cancelled.";
    default:
      return "This order's pickup pass isn't available.";
  }
}

// Ports OrderPickupPass.razor (/orders/pickup/:id) — client-side QR generation, payload format unchanged: {origin}/orders/validate/{orderId}/{passId}.
function OrderPickupPass() {
  const { id } = useParams<{ id: string }>();
  const timeZone = useTimeZone();

  const [order, setOrder] = useState<OrderDto | null>(null);
  const [forbidden, setForbidden] = useState(false);
  const [notFound, setNotFound] = useState(false);
  const [selectedPass, setSelectedPass] = useState<PickupPassDto | null>(null);
  const [qrSvg, setQrSvg] = useState<string | null>(null);
  const [splitting, setSplitting] = useState(false);
  const [splitBusy, setSplitBusy] = useState(false);
  const [splitError, setSplitError] = useState<string | null>(null);
  const [passCount, setPassCount] = useState(MIN_PASSES);

  async function load(keepSelectedId: string | null) {
    if (!id) return;
    try {
      const loaded = await ordersApi.getMineById(id);
      setOrder(loaded);

      if (loaded.status === "Confirmed" && loaded.pickupPasses.length > 0) {
        const passes = [...loaded.pickupPasses].sort((a, b) => a.createdAt.localeCompare(b.createdAt));
        const next = passes.find((p) => p.id === keepSelectedId) ?? passes[0];
        selectPass(loaded.id, next);
        // Keeps the split-count selector in sync with reality — otherwise "Update passes" would
        // silently regenerate a stale count and invalidate already-issued QR codes.
        setPassCount(passes.length);
      }
    } catch (err) {
      if (err instanceof ApiError && err.status === 403) setForbidden(true);
      else if (err instanceof ApiError && err.status === 404) setNotFound(true);
    }
  }

  function selectPass(orderId: string, pass: PickupPassDto) {
    setSelectedPass(pass);
    const payloadUrl = `${window.location.origin}/orders/validate/${orderId}/${pass.id}`;
    QRCode.toString(payloadUrl, { type: "svg", errorCorrectionLevel: "Q", margin: 2, color: { dark: "#0b1f13", light: "#ffffff" } })
      .then(setQrSvg)
      .catch(() => setQrSvg(null));
  }

  useEffect(() => {
    void load(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function split() {
    if (!id) return;
    setSplitBusy(true);
    setSplitError(null);
    try {
      await ordersApi.splitPickupPasses(id, passCount);
      setSelectedPass(null);
      await load(null);
      setSplitting(false);
    } catch (err) {
      setSplitError(err instanceof ApiError ? err.message : "Couldn't update pickup passes.");
    } finally {
      setSplitBusy(false);
    }
  }

  return (
    <div className="pickup-pass-page">
      <Link to="/orders" className="biz-page-back">
        <i className="bi bi-arrow-left" /> Back to your orders
      </Link>

      {forbidden ? (
        <ForbiddenPanel message="You can only view the pickup pass for your own orders." backHref="/orders" backLabel="Back to your orders" />
      ) : notFound ? (
        <NotFoundPanel message="This order no longer exists." backHref="/orders" backLabel="Back to your orders" />
      ) : order === null ? (
        <div className="d-flex justify-content-center py-5">
          <div className="spinner-border text-primary" role="status">
            <span className="visually-hidden">Loading...</span>
          </div>
        </div>
      ) : order.status !== "Confirmed" ? (
        <div className="orders-empty">
          <div className="em-empty-icon">
            <i className="bi bi-qr-code-scan" />
          </div>
          <p className="mb-1">No pickup pass to show.</p>
          <span className="text-muted small d-block">{statusExplanation(order.status)}</span>
        </div>
      ) : selectedPass !== null ? (
        (() => {
          const passes = [...order.pickupPasses].sort((a, b) => a.createdAt.localeCompare(b.createdAt));
          const total = order.lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0);
          const kgSaved = order.lines.reduce((sum, l) => sum + l.quantity * l.weightKg, 0);

          return (
            <>
              {passes.length > 1 && (
                <>
                  <div className="pickup-pass-switcher" role="tablist" aria-label="Pickup passes">
                    {passes.map((pass) => (
                      <button
                        type="button"
                        className={`pickup-pass-tab ${pass.id === selectedPass.id ? "active" : ""}`}
                        role="tab"
                        aria-selected={pass.id === selectedPass.id}
                        key={pass.id}
                        onClick={() => selectPass(order.id, pass)}
                      >
                        {pass.label}
                      </button>
                    ))}
                  </div>
                  <p className="pickup-pass-switcher-hint">
                    Each pass is its own QR code — hand a different one to each person picking up, whoever gets there first scans to complete
                    the order.
                  </p>
                </>
              )}

              <article className="order-ticket order-ticket--pass">
                <div className="order-ticket-main">
                  <div className="order-ticket-top">
                    <div className="order-ticket-kitchen">
                      <div className="order-ticket-business">
                        <i className="bi bi-shop" />
                        {order.businessName}
                      </div>
                    </div>
                    <span className="order-status-badge order-status-confirmed">Confirmed</span>
                  </div>

                  <div className="order-ticket-items">
                    {order.lines.map((line) => (
                      <div className="order-ticket-line" key={line.packageId}>
                        <span className="order-ticket-line-qty">{line.quantity}&times;</span>
                        <span className="order-ticket-line-name">{line.packageName}</span>
                        <span className="order-ticket-line-price">{formatCurrency(line.quantity * line.unitPrice)}</span>
                      </div>
                    ))}
                  </div>

                  <div className="order-ticket-footer">
                    <div className="order-ticket-pickup">
                      <i className="bi bi-clock" />
                      Pickup {formatOrderPickupWindow(order.lines, timeZone)}
                    </div>
                    <div className="order-ticket-total">
                      <span>Total</span>
                      <span className="order-ticket-total-value">{formatCurrency(total)}</span>
                    </div>
                  </div>

                  {kgSaved > 0 && (
                    <div className="order-ticket-impact">
                      <i className="bi bi-leaf" /> You&apos;re about to save ~{kgSaved.toFixed(1).replace(/\.0$/, "")} kg of food from waste
                    </div>
                  )}
                </div>

                <div className="order-ticket-seam" />

                <div className="order-ticket-stub order-ticket-stub--pass">
                  <span className="order-ticket-stub-label">Order</span>
                  <span className="order-ticket-stub-number">#{String(order.orderNumber).padStart(3, "0")}</span>
                  {qrSvg && <div className="order-ticket-stub-qr" dangerouslySetInnerHTML={{ __html: qrSvg }} />}
                  {passes.length > 1 && <span className="order-ticket-stub-pass-label">{selectedPass.label}</span>}
                  <span className="order-ticket-stub-hint">Hand your phone to the counter — they&apos;ll scan this to confirm pickup</span>
                </div>
              </article>

              <div className="pickup-pass-split">
                {!splitting ? (
                  <button type="button" className="pickup-pass-split-toggle" onClick={() => setSplitting(true)}>
                    <i className="bi bi-people" /> Splitting with a group? Get separate passes
                  </button>
                ) : (
                  <>
                    <div className="pickup-pass-split-form">
                      <label htmlFor="passCount">Number of passes</label>
                      <select id="passCount" className="form-select form-select-sm" value={passCount} onChange={(e) => setPassCount(Number(e.target.value))}>
                        {Array.from({ length: MAX_PASSES - MIN_PASSES + 1 }, (_, i) => MIN_PASSES + i).map((n) => (
                          <option value={n} key={n}>
                            {n}
                          </option>
                        ))}
                      </select>
                      <button type="button" className="btn btn-primary btn-sm" disabled={splitBusy} onClick={() => void split()}>
                        {splitBusy && <span className="spinner-border spinner-border-sm me-1" role="status" />}
                        Update passes
                      </button>
                      <button type="button" className="btn btn-link btn-sm" disabled={splitBusy} onClick={() => setSplitting(false)}>
                        Cancel
                      </button>
                    </div>
                    {splitError && (
                      <div className="alert alert-danger py-2 small mt-2" role="alert">
                        {splitError}
                      </div>
                    )}
                  </>
                )}
              </div>
            </>
          );
        })()
      ) : null}
    </div>
  );
}

export default OrderPickupPass;
