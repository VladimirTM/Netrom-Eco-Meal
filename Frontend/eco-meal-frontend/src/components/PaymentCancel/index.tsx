import { Link } from "react-router-dom";

// Ports PaymentCancel.razor (/checkout/cancel) — static, no API call. Nothing was charged and the
// basket (which StartCheckout never cleared) is still there if the customer wants to try again.
function PaymentCancel() {
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

        <div className="cart-confirmation">
          <i className="bi bi-x-circle text-muted" />
          <h3>Payment cancelled</h3>
          <p>Nothing was charged. Your basket is still saved if you&apos;d like to try again.</p>
          <Link className="btn btn-primary w-100 py-2 fw-semibold" to="/">
            Back to browsing
          </Link>
        </div>
      </div>
    </div>
  );
}

export default PaymentCancel;
