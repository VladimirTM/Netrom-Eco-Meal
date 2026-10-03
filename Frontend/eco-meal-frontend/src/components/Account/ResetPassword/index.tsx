import { useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { authApi } from "../../../api/clients/AuthApiClient";
import { ApiError } from "../../../api/base/http";

function ResetPassword() {
  const [searchParams] = useSearchParams();
  const email = searchParams.get("email");
  const token = searchParams.get("token");

  const [newPassword, setNewPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [succeeded, setSucceeded] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (newPassword.length < 8) {
      setError("Password must be at least 8 characters.");
      return;
    }

    setBusy(true);
    setError(null);
    try {
      await authApi.resetPassword(email!, token!, newPassword);
      setSucceeded(true);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Please try again.");
    } finally {
      setBusy(false);
    }
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

        <h1 className="login-heading">Reset password</h1>

        {!email || !token ? (
          <div className="alert alert-danger py-2 small mb-3" role="alert">This reset link is invalid.</div>
        ) : succeeded ? (
          <>
            <div className="alert alert-success py-2 small mb-3" role="alert">Your password has been reset.</div>
            <Link className="btn btn-primary w-100 py-2 fw-semibold" to="/account/login">Sign in</Link>
          </>
        ) : (
          <form onSubmit={handleSubmit}>
            <div className="mb-4">
              <label className="form-label fw-semibold" htmlFor="reset-password">New password</label>
              <input
                id="reset-password"
                className="form-control"
                type="password"
                placeholder="••••••••"
                minLength={8}
                autoComplete="new-password"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
              />
            </div>
            {error && <div className="alert alert-danger py-2 small mb-3" role="alert">{error}</div>}
            <button className="btn btn-primary w-100 py-2 fw-semibold" type="submit" disabled={busy}>
              {busy ? "Resetting…" : "Reset password"}
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

export default ResetPassword;
