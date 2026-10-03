import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { rescueCirclesApi } from "../../api/clients/RescueCirclesApiClient";
import type { RescueCircleSummary } from "../../api/models/RescueCircle";

// Ports RescueCircleReturn.razor (/circles/return) — RescueCircleService.cs builds this exact
// path into each participant's own Stripe success_url.
function RescueCircleReturn() {
  const [searchParams] = useSearchParams();
  const circle = searchParams.get("circle");
  const participant = searchParams.get("participant");
  const sessionId = searchParams.get("session_id");

  const [loading, setLoading] = useState(Boolean(circle && participant && sessionId));
  const [success, setSuccess] = useState(false);
  const [message, setMessage] = useState("This payment link is invalid.");
  const [summary, setSummary] = useState<RescueCircleSummary | null>(null);

  useEffect(() => {
    if (!circle || !participant || !sessionId) return;

    (async () => {
      try {
        const result = await rescueCirclesApi.completeShareCheckout(circle, participant, sessionId);
        if (result.success) {
          setSuccess(true);
          setSummary(result.summary);
        } else {
          setMessage(result.message);
        }
      } catch {
        setMessage("This payment couldn't be confirmed.");
      } finally {
        setLoading(false);
      }
    })();
  }, [circle, participant, sessionId]);

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
            <p className="mb-0">Confirming your share…</p>
          </div>
        ) : success ? (
          <div className="cart-confirmation">
            <i className="bi bi-check-circle-fill" />
            <h3>Your share is paid</h3>
            {summary && summary.paidCount === summary.participantCount ? (
              <p>Everyone&apos;s paid — {summary.businessName} will confirm the order soon.</p>
            ) : summary ? (
              <p>
                {summary.paidCount} of {summary.participantCount} people have paid so far. Share the link so the rest can chip in.
              </p>
            ) : null}
            <Link className="btn btn-primary w-100 py-2 fw-semibold" to={`/circles/${circle}`}>
              View the Rescue Circle
            </Link>
          </div>
        ) : (
          <>
            <h1 className="login-heading">Payment couldn&apos;t be confirmed</h1>
            <div className="alert alert-danger py-2 small mb-3" role="alert">
              {message}
            </div>
            <Link className="btn btn-primary w-100 py-2 fw-semibold" to={`/circles/${circle}`}>
              Back to the Rescue Circle
            </Link>
          </>
        )}
      </div>
    </div>
  );
}

export default RescueCircleReturn;
