import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { paymentsApi } from "../../api/clients/PaymentsApiClient";
import type { OrderDto } from "../../api/models/Order";
import { useCart } from "../../context/CartContext/cart-context";

// Ports PaymentReturn.razor (/checkout/return) — CheckoutService.cs builds this exact path into
// Stripe's success_url, so it must not change. Idempotent: replaying this page with the same
// pc/session_id (e.g. a reload) returns the same already-created order instead of double-placing.
function PaymentReturn() {
  const [searchParams] = useSearchParams();
  const cart = useCart();
  const pc = searchParams.get("pc");
  const sessionId = searchParams.get("session_id");

  const [loading, setLoading] = useState(true);
  const [success, setSuccess] = useState(false);
  const [message, setMessage] = useState("This payment link is invalid.");
  const [order, setOrder] = useState<OrderDto | null>(null);
  const [kgSaved, setKgSaved] = useState<number | null>(null);

  useEffect(() => {
    if (!pc || !sessionId) {
      setLoading(false);
      return;
    }

    (async () => {
      try {
        const result = await paymentsApi.complete(pc, sessionId);
        if (result.success) {
          setSuccess(true);
          setOrder(result.order);
          setKgSaved(result.kgSaved);
          // The basket that was checked out is now a real order — clear it so it doesn't linger
          // and get re-submitted from a stale browser tab.
          cart.clear();
        } else {
          setMessage(result.message);
        }
      } catch {
        setMessage("This payment doesn't belong to your account, or couldn't be confirmed.");
      } finally {
        setLoading(false);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pc, sessionId]);

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-brand">
          <img src="/logo.png" alt="Eco Meal" className="login-logo" />
          <div>
            <div className="login-brand-name">Eco Meal</div>
            <div className="login-brand-sub">Surplus food, rescued</div>
          </div>
        </div>

        {loading ? (
          <div className="cart-confirmation">
            <div className="spinner-border text-primary mb-3" role="status">
              <span className="visually-hidden">Confirming payment...</span>
            </div>
            <p className="mb-0">Confirming your payment…</p>
          </div>
        ) : success ? (
          <div className="cart-confirmation">
            <i className="bi bi-check-circle-fill" />
            <h3>Order #{String(order?.orderNumber).padStart(3, "0")} placed</h3>
            <p>{order?.businessName} has your order — head over at pickup time to collect it.</p>
            {kgSaved !== null && kgSaved > 0 && (
              <p className="cart-confirmation-impact">
                <i className="bi bi-leaf" /> ~{kgSaved.toFixed(1).replace(/\.0$/, "")} kg of food saved from waste
              </p>
            )}
            <Link className="btn btn-primary w-100 py-2 fw-semibold" to="/orders">
              View my orders
            </Link>
          </div>
        ) : (
          <>
            <h1 className="login-heading">Payment couldn&apos;t be confirmed</h1>
            <div className="alert alert-danger py-2 small mb-3" role="alert">
              {message}
            </div>
            <Link className="btn btn-primary w-100 py-2 fw-semibold" to="/">
              Back to browsing
            </Link>
          </>
        )}
      </div>
    </div>
  );
}

export default PaymentReturn;
