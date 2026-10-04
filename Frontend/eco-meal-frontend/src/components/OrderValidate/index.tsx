import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ApiError } from "../../api/base/http";
import { ordersApi } from "../../api/clients/OrdersApiClient";
import type { OrderDto, PickupPassDto } from "../../api/models/Order";
import { useTimeZone } from "../../context/TimeZoneContext/timezone-context";
import ForbiddenPanel from "../common/ForbiddenPanel";
import NotFoundPanel from "../common/NotFoundPanel";
import { formatCurrency } from "../../utils/currency";
import { formatOrderPickupWindow } from "../../utils/packagePickup";
import { orderStatusCssSuffix, orderStatusLabel } from "../../utils/orderStatus";

function statusExplanation(order: OrderDto, pass: PickupPassDto): string {
  switch (order.status) {
    case "Pending":
      return "This order hasn't been confirmed by the business yet.";
    case "Completed":
      return pass.redeemedAt === null ? "This order was already picked up using a different pass." : "This order was already picked up.";
    case "Cancelled":
      return "This order was cancelled.";
    default:
      return "This order can't be completed right now.";
  }
}

// Ports OrderValidate.razor (/orders/validate/:id/:passId) — reachable both from the in-app
// scanner and by any external QR reader opening this URL directly, so it must re-check
// authorization itself, never trust how the visitor arrived.
function OrderValidate() {
  const { id, passId } = useParams<{ id: string; passId: string }>();
  const timeZone = useTimeZone();
  const [order, setOrder] = useState<OrderDto | null>(null);
  const [forbidden, setForbidden] = useState(false);
  const [notFound, setNotFound] = useState(false);
  const [busy, setBusy] = useState(false);
  const [completed, setCompleted] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!id) return;
    try {
      const loaded = await ordersApi.getForManagementById(id);
      setOrder(loaded);
    } catch (err) {
      if (err instanceof ApiError && err.status === 403) setForbidden(true);
      else setNotFound(true);
    }
  }, [id]);

  useEffect(() => {
    // Synchronizes with the server for this order/pass pair.
    // oxlint-disable-next-line react/set-state-in-effect
    load();
  }, [load]);

  const pass = order?.pickupPasses.find((p) => p.id === passId) ?? null;

  async function confirmPickup() {
    if (!id || !passId) return;
    setBusy(true);
    setActionError(null);
    try {
      const updated = await ordersApi.redeemPickupPass(id, passId);
      setOrder(updated);
      setCompleted(true);
    } catch (err) {
      // Someone else beat us to it (duplicate scan, another pass redeemed, manual complete,
      // cancellation) — refresh from the server rather than leaving a stale status badge on screen.
      setActionError(err instanceof ApiError ? err.message : "This order couldn't be completed.");
      await load();
    } finally {
      setBusy(false);
    }
  }

  if (forbidden) {
    return <ForbiddenPanel message="You don't manage this order's business." backHref="/orders/manage" backLabel="Back to orders" />;
  }
  if (notFound) {
    return <NotFoundPanel message="This order no longer exists." backHref="/orders/manage" backLabel="Back to orders" />;
  }

  return (
    <div className="validate-page">
      {!order ? (
        <div className="d-flex justify-content-center py-5">
          <div className="spinner-border text-primary" role="status">
            <span className="visually-hidden">Loading...</span>
          </div>
        </div>
      ) : !pass ? (
        <NotFoundPanel message="This pickup pass doesn't exist." backHref="/orders/manage" backLabel="Back to orders" />
      ) : (
        <div className="card border-0 shadow-sm validate-card">
          <div className="d-flex justify-content-between align-items-start mb-3">
            <div>
              <span className="biz-modal-eyebrow">
                Order #{String(order.orderNumber).padStart(3, "0")}
                {order.pickupPasses.length > 1 ? ` · ${pass.label}` : ""}
              </span>
              <h1 className="h4 fw-bold mb-0">{order.businessName}</h1>
            </div>
            <span className={`order-status-badge order-status-${orderStatusCssSuffix(order.status)}`}>{orderStatusLabel(order.status)}</span>
          </div>

          {actionError && (
            <div className="alert alert-danger" role="alert">
              {actionError}
            </div>
          )}

          {completed && (
            <div className="biz-modal-inline-toast">
              <i className="bi bi-check-circle-fill" /> Pickup confirmed
            </div>
          )}

          <div className="pkg-modal-facts">
            <div className="pkg-modal-fact">
              <i className="bi bi-person" />
              <div>
                <div className="pkg-modal-fact-label">Customer</div>
                <div className="pkg-modal-fact-value">{order.customerName}</div>
              </div>
            </div>
            <div className="pkg-modal-fact">
              <i className="bi bi-clock" />
              <div>
                <div className="pkg-modal-fact-label">Pickup window</div>
                <div className="pkg-modal-fact-value">{formatOrderPickupWindow(order.lines, timeZone)}</div>
              </div>
            </div>
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

          <div className="d-flex justify-content-between align-items-center validate-total">
            <span className="text-muted small fw-semibold">Total</span>
            <span className="order-ticket-total-value">{formatCurrency(order.lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0))}</span>
          </div>

          {order.status === "Confirmed" && pass.redeemedAt === null ? (
            <>
              {order.pickupPasses.length > 1 && (
                <p className="text-muted small mb-2">
                  <i className="bi bi-people" /> This order has {order.pickupPasses.length} pickup passes — confirming this one completes the whole order.
                </p>
              )}
              <button type="button" className="order-action-btn order-action-complete validate-confirm-btn" disabled={busy} onClick={confirmPickup}>
                {busy && <span className="spinner-border spinner-border-sm me-2" role="status" />}
                <i className="bi bi-check2-all" /> Confirm pickup
              </button>
            </>
          ) : (
            !completed && <p className="text-muted small mb-0">{statusExplanation(order, pass)}</p>
          )}

          <Link to="/orders/scan" className="validate-scan-next">
            Scan another order
          </Link>
        </div>
      )}
    </div>
  );
}

export default OrderValidate;
