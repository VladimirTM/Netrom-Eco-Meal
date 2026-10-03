import { useState } from "react";
import { ApiError } from "../../../api/base/http";
import { paymentsApi } from "../../../api/clients/PaymentsApiClient";
import { rescueCirclesApi } from "../../../api/clients/RescueCirclesApiClient";
import { useCart } from "../../../context/CartContext/cart-context";
import { formatCurrency } from "../../../utils/currency";

// Mirrors RescueCircles/Checkout constants (Backend/NetromEcoMeal.DataAccess/Constants) — kept in
// sync manually since the frontend has no access to the backend's compiled constants.
const MIN_PARTICIPANTS = 2;
const MAX_PARTICIPANTS = 6;
const MIN_CHARGEABLE_AMOUNT = 2.0;
const MAX_NOTE_LENGTH = 300;

// Ports CartPanel.razor — the basket slide-over, with an optional "split with friends" hop into
// starting a Rescue Circle instead of a solo checkout.
function CartPanel() {
  const cart = useCart();
  const [placing, setPlacing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [splitOpen, setSplitOpen] = useState(false);
  const [participantCount, setParticipantCount] = useState(MIN_PARTICIPANTS);

  if (!cart.isOpen) return null;

  const estimatedShareTooSmall = participantCount > 0 && cart.totalPrice / participantCount < MIN_CHARGEABLE_AMOUNT;

  // Sends the browser to Stripe's hosted Checkout page — the cart itself is only cleared once
  // payment is confirmed on /checkout/return, since the order shouldn't be considered placed
  // until then.
  async function startCheckout() {
    if (!cart.businessId || placing) return;
    setPlacing(true);
    setError(null);

    try {
      const url = await paymentsApi.createCheckoutSession(
        cart.businessId,
        cart.items.map((i) => ({ packageId: i.packageId, quantity: i.quantity })),
        cart.logisticsNote || null,
      );
      window.location.href = url;
    } catch (err) {
      setPlacing(false);
      setError(err instanceof ApiError ? err.message : "We couldn't start checkout. Please try again.");
    }
  }

  // Unlike a solo checkout, a Rescue Circle's order is placed immediately — the group hasn't
  // finished paying yet, but the basket has already become a real (Pending) order, so the cart
  // is cleared right away instead of waiting for /circles/return.
  async function startCircle() {
    if (!cart.businessId || placing) return;
    setPlacing(true);
    setError(null);

    try {
      const url = await rescueCirclesApi.startCircle(
        cart.businessId,
        cart.items.map((i) => ({ packageId: i.packageId, quantity: i.quantity })),
        participantCount,
        cart.logisticsNote || null,
      );
      cart.clear();
      window.location.href = url;
    } catch (err) {
      setPlacing(false);
      setError(err instanceof ApiError ? err.message : "We couldn't start this Rescue Circle. Please try again.");
    }
  }

  return (
    <>
      <div className="cart-backdrop" onClick={cart.close} />
      <aside className="cart-panel">
        <div className="cart-panel-header">
          <div>
            <h2 className="cart-panel-title">Your basket</h2>
            {cart.businessName && (
              <p className="cart-panel-subtitle">
                <i className="bi bi-shop" /> {cart.businessName}
              </p>
            )}
          </div>
          <button type="button" className="cart-panel-close" onClick={cart.close} aria-label="Close basket">
            <i className="bi bi-x-lg" />
          </button>
        </div>

        {cart.items.length === 0 ? (
          <div className="cart-panel-body cart-empty">
            <div className="em-empty-icon">
              <i className="bi bi-basket2" />
            </div>
            <p className="mb-1">Your basket is empty.</p>
            <span className="text-muted small">Add a package from a kitchen to get started.</span>
          </div>
        ) : (
          <>
            <div className="cart-panel-body">
              {error && <div className="alert alert-danger py-2 small mb-3">{error}</div>}
              {cart.items.map((item) => (
                <div className="cart-line" key={item.packageId}>
                  <div className="cart-line-info">
                    <div className="cart-line-name">{item.name}</div>
                    <div className="cart-line-price">{formatCurrency(item.unitPrice)} each</div>
                  </div>
                  <div className="cart-line-controls">
                    <button type="button" className="cart-step-btn" onClick={() => cart.setQuantity(item.packageId, item.quantity - 1)}>
                      <i className="bi bi-dash" />
                    </button>
                    <span className="cart-line-qty">{item.quantity}</span>
                    <button
                      type="button"
                      className="cart-step-btn"
                      disabled={item.quantity >= item.maxQuantity}
                      onClick={() => cart.setQuantity(item.packageId, item.quantity + 1)}
                    >
                      <i className="bi bi-plus" />
                    </button>
                  </div>
                  <button type="button" className="cart-line-remove" title="Remove" onClick={() => cart.removeItem(item.packageId)}>
                    <i className="bi bi-trash" />
                  </button>
                </div>
              ))}
            </div>

            <div className="cart-panel-footer">
              <div className="cart-panel-total">
                <span>Total</span>
                <span className="cart-panel-total-value">{formatCurrency(cart.totalPrice)}</span>
              </div>

              <div className="mb-2">
                <label htmlFor="cart-logistics-note" className="form-label small text-muted mb-1">
                  Note for the business (optional)
                </label>
                <textarea
                  id="cart-logistics-note"
                  className="form-control form-control-sm"
                  rows={2}
                  maxLength={MAX_NOTE_LENGTH}
                  placeholder="Running late, can't carry it to my car, will call when I arrive…"
                  value={cart.logisticsNote}
                  onChange={(e) => cart.setLogisticsNote(e.target.value)}
                />
              </div>

              {!splitOpen ? (
                <>
                  <button type="button" className="btn btn-primary w-100 py-2 mb-2" disabled={placing} onClick={() => void startCheckout()}>
                    {placing ? <span className="spinner-border spinner-border-sm me-2" role="status" /> : <i className="bi bi-credit-card me-2" />}
                    Pay &amp; place order
                  </button>
                  <button type="button" className="btn btn-link w-100 py-0 text-decoration-none small" onClick={() => setSplitOpen(true)}>
                    <i className="bi bi-people me-1" />
                    Split this with friends instead
                  </button>
                </>
              ) : (
                <div className="rescue-circle-starter mb-2">
                  <div className="d-flex align-items-center justify-content-between mb-2">
                    <span className="small fw-semibold">
                      <i className="bi bi-people me-1" />
                      Rescue Circle
                    </span>
                    <button type="button" className="btn-close btn-sm" aria-label="Close" onClick={() => setSplitOpen(false)} />
                  </div>
                  <p className="text-muted small mb-2">
                    Split the {formatCurrency(cart.totalPrice)} total evenly — everyone pays their own share, and you&apos;ll get a link to invite
                    them.
                  </p>
                  <div className="d-flex align-items-center gap-2 mb-2">
                    <button
                      type="button"
                      className="cart-step-btn"
                      disabled={participantCount <= MIN_PARTICIPANTS}
                      onClick={() => setParticipantCount((n) => n - 1)}
                    >
                      <i className="bi bi-dash" />
                    </button>
                    <span className="cart-line-qty">{participantCount} people</span>
                    <button
                      type="button"
                      className="cart-step-btn"
                      disabled={participantCount >= MAX_PARTICIPANTS}
                      onClick={() => setParticipantCount((n) => n + 1)}
                    >
                      <i className="bi bi-plus" />
                    </button>
                    <span className="text-muted small ms-auto">≈ {formatCurrency(cart.totalPrice / participantCount)} each</span>
                  </div>
                  {estimatedShareTooSmall && (
                    <div className="alert alert-warning py-2 small mb-2">
                      Each share would come to less than {formatCurrency(MIN_CHARGEABLE_AMOUNT)} — try fewer people or a larger basket.
                    </div>
                  )}
                  <button
                    type="button"
                    className="btn btn-primary w-100 py-2"
                    disabled={placing || estimatedShareTooSmall}
                    onClick={() => void startCircle()}
                  >
                    {placing ? <span className="spinner-border spinner-border-sm me-2" role="status" /> : <i className="bi bi-credit-card me-2" />}
                    Start &amp; pay my share
                  </button>
                </div>
              )}
            </div>
          </>
        )}
      </aside>
    </>
  );
}

export default CartPanel;
