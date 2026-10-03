import { useState, type FormEvent } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { authApi } from "../../../api/clients/AuthApiClient";
import { useAuth } from "../../../context/AuthContext/auth-context";
import { ApiError } from "../../../api/base/http";

function Login() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { login } = useAuth();
  const returnUrl = searchParams.get("returnUrl");
  const info = searchParams.get("info");

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [unconfirmed, setUnconfirmed] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    setUnconfirmed(false);

    try {
      const response = await authApi.login({ email, password });
      login(response.token, response.user);
      navigate(returnUrl ? decodeURIComponent(returnUrl) : "/", { replace: true });
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
        setUnconfirmed(err.code === "email_not_confirmed");
      } else {
        setError("Something went wrong. Please try again.");
      }
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

        <h1 className="login-heading">Sign in</h1>

        {info && <div className="alert alert-success py-2 small mb-3" role="alert">{info}</div>}

        <form onSubmit={handleSubmit}>
          <div className="mb-3">
            <label className="form-label fw-semibold" htmlFor="login-email">Email</label>
            <input
              id="login-email"
              className="form-control"
              type="email"
              required
              placeholder="you@example.com"
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>
          <div className="mb-2">
            <label className="form-label fw-semibold" htmlFor="login-password">Password</label>
            <input
              id="login-password"
              className="form-control"
              type="password"
              required
              placeholder="••••••••"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>
          <p className="text-end mb-3">
            <Link className="small" to="/account/forgot-password">Forgot your password?</Link>
          </p>
          {error && (
            <div className="alert alert-danger py-2 small mb-3" role="alert">
              {error}
              {unconfirmed && (
                <span> <Link to="/account/resend-confirmation">Resend confirmation email</Link></span>
              )}
            </div>
          )}
          <button className="btn btn-primary w-100 py-2 fw-semibold" type="submit" disabled={busy}>
            {busy ? "Signing in…" : "Sign in"}
          </button>
        </form>

        <p className="login-footer-link">
          Don't have an account? <Link to="/account/register">Create one</Link>
        </p>
      </div>
    </div>
  );
}

export default Login;
