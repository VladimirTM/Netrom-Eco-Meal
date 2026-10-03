import { useState, type FormEvent } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { authApi } from "../../../api/clients/AuthApiClient";
import { useAuth } from "../../../context/AuthContext/auth-context";
import { ApiError } from "../../../api/base/http";

function Register() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { login } = useAuth();

  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  // Prefilled from a friend's /account/register?ref=CODE link — still editable
  // by hand for someone who was just told the code.
  const [referralCode, setReferralCode] = useState(searchParams.get("ref") ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);

    try {
      const response = await authApi.register({ name, email, password, referralCode });
      if (response.info) {
        // Confirmation required — Login already knows how to show an ?info= banner (see its own
        // `info` param handling), so land there instead of leaving the user stuck on this page.
        navigate(`/account/login?info=${encodeURIComponent(response.info)}`, { replace: true });
        return;
      }
      login(response.token!, response.user!);
      navigate("/", { replace: true });
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

        <h1 className="login-heading">Create account</h1>

        <form onSubmit={handleSubmit}>
          <div className="mb-3">
            <label className="form-label fw-semibold" htmlFor="register-name">
              Full name
            </label>
            <input
              id="register-name"
              className="form-control"
              type="text"
              required
              placeholder="Jane Doe"
              autoComplete="name"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </div>
          <div className="mb-3">
            <label className="form-label fw-semibold" htmlFor="register-email">
              Email
            </label>
            <input
              id="register-email"
              className="form-control"
              type="email"
              required
              placeholder="you@example.com"
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>
          <div className="mb-4">
            <label className="form-label fw-semibold" htmlFor="register-password">
              Password
            </label>
            <input
              id="register-password"
              className="form-control"
              type="password"
              required
              placeholder="••••••••"
              autoComplete="new-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>
          <div className="mb-4">
            <label className="form-label fw-semibold" htmlFor="register-referral">
              Referral code <span className="text-muted fw-normal">(optional)</span>
            </label>
            <input
              id="register-referral"
              className="form-control"
              type="text"
              placeholder="Got a code from a friend?"
              value={referralCode}
              onChange={(e) => setReferralCode(e.target.value)}
            />
          </div>
          {error && (
            <div className="alert alert-danger py-2 small mb-3" role="alert">
              {error}
            </div>
          )}
          <button className="btn btn-primary w-100 py-2 fw-semibold" type="submit" disabled={busy}>
            {busy ? "Creating account…" : "Create account"}
          </button>
        </form>

        <p className="login-footer-link">
          Already have an account? <Link to="/account/login">Sign in</Link>
        </p>
      </div>
    </div>
  );
}

export default Register;
