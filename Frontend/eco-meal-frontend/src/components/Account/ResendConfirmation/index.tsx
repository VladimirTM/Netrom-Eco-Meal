import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { authApi } from "../../../api/clients/AuthApiClient";

function ResendConfirmation() {
  const [email, setEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!email.trim()) return;

    setBusy(true);
    await authApi.resendConfirmation(email.trim()).catch(() => {});
    setBusy(false);
    setSubmitted(true);
  }

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

        <h1 className="login-heading">Resend confirmation email</h1>

        {submitted ? (
          <div className="alert alert-success py-2 small mb-3" role="alert">
            If an unconfirmed account exists for that email, we've sent a fresh confirmation link.
          </div>
        ) : (
          <form onSubmit={handleSubmit}>
            <p className="text-muted small mb-3">
              Lost the email, or the link expired? Enter your account email and we'll send a new one.
            </p>
            <div className="mb-4">
              <label className="form-label fw-semibold" htmlFor="resend-email">Email</label>
              <input
                id="resend-email"
                className="form-control"
                type="email"
                placeholder="you@example.com"
                autoComplete="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>
            <button className="btn btn-primary w-100 py-2 fw-semibold" type="submit" disabled={busy}>
              {busy ? "Sending…" : "Resend confirmation email"}
            </button>
          </form>
        )}

        <p className="login-footer-link">
          <Link to="/account/login">Back to sign in</Link>
        </p>
      </div>
    </div>
  );
}

export default ResendConfirmation;
