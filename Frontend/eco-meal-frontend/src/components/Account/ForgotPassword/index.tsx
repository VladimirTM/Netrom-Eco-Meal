import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { authApi } from "../../../api/clients/AuthApiClient";

function ForgotPassword() {
  const [email, setEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!email.trim()) return;

    setBusy(true);
    await authApi.forgotPassword(email.trim()).catch(() => {});
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

        <h1 className="login-heading">Forgot password</h1>

        {submitted ? (
          <div className="alert alert-success py-2 small mb-3" role="alert">
            If an account exists for that email, we've sent a link to reset your password.
          </div>
        ) : (
          <form onSubmit={handleSubmit}>
            <p className="text-muted small mb-3">
              Enter your account email and we'll send you a link to reset your password.
            </p>
            <div className="mb-4">
              <label className="form-label fw-semibold" htmlFor="forgot-email">Email</label>
              <input
                id="forgot-email"
                className="form-control"
                type="email"
                placeholder="you@example.com"
                autoComplete="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>
            <button className="btn btn-primary w-100 py-2 fw-semibold" type="submit" disabled={busy}>
              {busy ? "Sending…" : "Send reset link"}
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

export default ForgotPassword;
