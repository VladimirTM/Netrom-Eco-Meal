import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ApiError } from "../../api/base/http";
import { rescueCirclesApi } from "../../api/clients/RescueCirclesApiClient";
import type { RescueCircleDetailDto, RescueCircleSummary } from "../../api/models/RescueCircle";
import NotFoundPanel from "../common/NotFoundPanel";
import { useAuth } from "../../context/AuthContext/auth-context";
import { formatCurrency } from "../../utils/currency";

function statusLabel(summary: RescueCircleSummary | null): string {
  if (!summary) return "";
  if (summary.status === "Cancelled") return "Cancelled";
  return summary.paidCount === summary.participantCount ? "Fully paid" : "Collecting payments";
}

function statusBadgeClass(summary: RescueCircleSummary | null): string {
  if (!summary) return "";
  if (summary.status === "Cancelled") return "bg-secondary-subtle text-secondary";
  return summary.paidCount === summary.participantCount ? "bg-success-subtle text-success" : "bg-warning-subtle text-warning";
}

// Ports RescueCircleInvite.razor (/circles/:circleId) — despite the route name this is the
// detail/join/pay page, shareable as the invite link anyone signed in can open.
function RescueCircleInvite() {
  const { circleId } = useParams<{ circleId: string }>();
  const { user } = useAuth();

  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [summary, setSummary] = useState<RescueCircleSummary | null>(null);
  const [detail, setDetail] = useState<RescueCircleDetailDto | null>(null);

  const myParticipant = detail?.participants.find((p) => p.userId === user?.id) ?? null;

  async function load() {
    if (!circleId) return;
    try {
      const loadedDetail = await rescueCirclesApi.getDetail(circleId);
      setDetail(loadedDetail);
      setSummary(loadedDetail.summary);
    } catch (err) {
      if (err instanceof ApiError && err.status === 403) {
        // Not the organizer/a participant (GetDetailAsync throws UnauthorizedAccessException,
        // mapped to 403 by the Api's generic exception middleware) — fall back to the public
        // summary, clearing stale detail (e.g. right after leaving) so its participant list
        // doesn't linger.
        setDetail(null);
        try {
          setSummary(await rescueCirclesApi.getSummary(circleId));
        } catch {
          setNotFound(true);
        }
      } else {
        setNotFound(true);
      }
    }
    setLoading(false);
  }

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [circleId]);

  async function joinOrPay() {
    if (!circleId) return;
    setBusy(true);
    setError(null);
    try {
      window.location.href = await rescueCirclesApi.joinOrPay(circleId);
    } catch (err) {
      setBusy(false);
      setError(err instanceof ApiError ? err.message : "We couldn't process that — please try again.");
    }
  }

  async function leave() {
    if (!circleId) return;
    setBusy(true);
    setError(null);
    try {
      await rescueCirclesApi.leave(circleId);
      setMessage("You've left this Rescue Circle.");
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "We couldn't process that — please try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <section className="orders-hero">
        <div className="orders-hero-inner">
          <span className="orders-hero-eyebrow">
            <i className="bi bi-people" /> Rescue Circle
          </span>
          {summary ? (
            <>
              <h1 className="orders-hero-title">{summary.businessName}</h1>
              <p className="orders-hero-sub">
                Splitting {formatCurrency(summary.totalAmount)} across {summary.participantCount} people.
              </p>
            </>
          ) : (
            <h1 className="orders-hero-title">Rescue Circle</h1>
          )}
        </div>
      </section>

      <section className="orders-list-section" style={{ maxWidth: 640 }}>
        {loading ? (
          <div className="d-flex justify-content-center py-5">
            <div className="spinner-border text-primary" role="status">
              <span className="visually-hidden">Loading...</span>
            </div>
          </div>
        ) : notFound ? (
          <NotFoundPanel title="Rescue Circle not found" message="This Rescue Circle no longer exists." backHref="/" backLabel="Back to browsing" />
        ) : summary ? (
          <>
            {error && (
              <div className="alert alert-danger py-2 small mb-3" role="alert">
                {error}
              </div>
            )}
            {message && (
              <div className="alert alert-success py-2 small mb-3" role="alert">
                {message}
              </div>
            )}

            <div className="card mb-3">
              <div className="card-body">
                <div className="d-flex justify-content-between align-items-center mb-2">
                  <span className={`badge ${statusBadgeClass(summary)}`}>{statusLabel(summary)}</span>
                  <span className="text-muted small">
                    {summary.joinedCount} of {summary.participantCount} joined · {summary.paidCount} paid
                  </span>
                </div>
                <div className="progress mb-3" style={{ height: 8 }}>
                  <div
                    className="progress-bar bg-success"
                    style={{ width: `${summary.participantCount === 0 ? 0 : (summary.paidCount * 100) / summary.participantCount}%` }}
                  />
                </div>

                {detail && (
                  <ul className="list-group list-group-flush mb-3">
                    {detail.participants.map((p) => (
                      <li className="list-group-item d-flex justify-content-between align-items-center px-0" key={p.userId}>
                        <span>
                          {p.userName}
                          {p.isOrganizer && <span className="badge bg-secondary-subtle text-secondary ms-1">Organizer</span>}
                        </span>
                        <span className="d-flex align-items-center gap-2">
                          <span className="text-muted small">{formatCurrency(p.shareAmount)}</span>
                          {p.refundedAt ? (
                            <span className="badge bg-secondary-subtle text-secondary">
                              <i className="bi bi-arrow-counterclockwise" /> Refunded
                            </span>
                          ) : p.paidAt ? (
                            <span className="badge bg-success-subtle text-success">
                              <i className="bi bi-check-circle" /> Paid
                            </span>
                          ) : (
                            <span className="badge bg-warning-subtle text-warning">
                              <i className="bi bi-hourglass-split" /> Owes
                            </span>
                          )}
                        </span>
                      </li>
                    ))}
                  </ul>
                )}

                {summary.status === "Cancelled" ? (
                  <p className="text-muted small mb-0">This Rescue Circle was cancelled — anyone who&apos;d paid was refunded.</p>
                ) : summary.paidCount === summary.participantCount ? (
                  <p className="text-success small mb-0">
                    <i className="bi bi-check-circle-fill" /> Fully paid — waiting for {summary.businessName} to confirm the order.
                  </p>
                ) : myParticipant?.paidAt ? (
                  <>
                    <p className="text-muted small mb-2">
                      You&apos;ve paid your {formatCurrency(myParticipant.shareAmount)} share — waiting on {summary.participantCount - summary.paidCount} more.
                    </p>
                    {!myParticipant.isOrganizer && (
                      <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy} onClick={() => void leave()}>
                        Leave &amp; get refunded
                      </button>
                    )}
                  </>
                ) : myParticipant ? (
                  <>
                    <button type="button" className="btn btn-primary w-100 py-2" disabled={busy} onClick={() => void joinOrPay()}>
                      {busy && <span className="spinner-border spinner-border-sm me-2" />}
                      Pay my {formatCurrency(myParticipant.shareAmount)} share
                    </button>
                    {!myParticipant.isOrganizer && (
                      <button type="button" className="btn btn-outline-secondary btn-sm w-100 mt-2" disabled={busy} onClick={() => void leave()}>
                        Leave this circle
                      </button>
                    )}
                  </>
                ) : summary.joinedCount < summary.participantCount ? (
                  <button type="button" className="btn btn-primary w-100 py-2" disabled={busy} onClick={() => void joinOrPay()}>
                    {busy && <span className="spinner-border spinner-border-sm me-2" />}
                    Join &amp; pay my {formatCurrency(summary.shareAmount)} share
                  </button>
                ) : (
                  <p className="text-muted small mb-0">This Rescue Circle is full.</p>
                )}

                {summary.isMine && detail?.organizerId && (
                  <>
                    <hr />
                    <p className="text-muted small mb-0">
                      <i className="bi bi-link-45deg" /> Share this page&apos;s link to invite people.
                    </p>
                  </>
                )}
              </div>
            </div>

            <Link to="/orders" className="btn btn-link px-0">
              Back to my orders
            </Link>
          </>
        ) : null}
      </section>
    </>
  );
}

export default RescueCircleInvite;
