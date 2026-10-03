import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { authApi } from "../../../api/clients/AuthApiClient";
import { ApiError } from "../../../api/base/http";

function ConfirmEmail() {
  const [searchParams] = useSearchParams();
  const userId = searchParams.get("userId");
  const token = searchParams.get("token");

  const [error, setError] = useState<string | null>("This confirmation link is invalid.");
  // The confirmation token is single-use server-side: a second identical call fails even though
  // the first one already succeeded. Without this guard, StrictMode's double-invoked mount effect
  // fires both calls, and whichever settles last (the failing one) overwrites the success state.
  const requestedRef = useRef(false);

  useEffect(() => {
    if (!userId || !token || requestedRef.current) return;
    requestedRef.current = true;
    authApi
      .confirmEmail(userId, token)
      .then(() => setError(null))
      .catch((err) => setError(err instanceof ApiError ? err.message : "This confirmation link is invalid."));
  }, [userId, token]);

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

        <h1 className="login-heading">Confirm account</h1>

        {error === null ? (
          <div className="alert alert-success py-2 small mb-3" role="alert">
            Your account is confirmed — you can now sign in.
          </div>
        ) : (
          <div className="alert alert-danger py-2 small mb-3" role="alert">{error}</div>
        )}

        <Link className="btn btn-primary w-100 py-2 fw-semibold" to="/account/login">
          Sign in
        </Link>
      </div>
    </div>
  );
}

export default ConfirmEmail;
