import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { referralsApi } from "../../api/clients/ReferralsApiClient";
import type { ReferralInfo } from "../../api/models/Referral";
import { formatCurrency } from "../../utils/currency";

// Ports Referrals.razor (/referrals) — share link, store-credit balance, invited-friends table.
function Referrals() {
  const [info, setInfo] = useState<ReferralInfo | null>(null);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    (async () => setInfo(await referralsApi.getMine()))();
  }, []);

  const referralLink = info ? `${window.location.origin}/account/register?ref=${info.referralCode}` : "";

  function copyLink() {
    navigator.clipboard
      .writeText(referralLink)
      .then(() => {
        setCopied(true);
        setTimeout(() => setCopied(false), 1500);
      })
      .catch(() => {});
  }

  return (
    <div className="container py-4">
      <div className="d-flex justify-content-between align-items-start mb-4">
        <div>
          <h1 className="h3 fw-bold mb-1">Invite a friend</h1>
          <p className="text-muted small mb-0">
            Share your link — once your friend completes their first rescue, you both get store credit toward a future order.
          </p>
        </div>
        <Link to="/" className="btn btn-outline-secondary">
          <i className="bi bi-arrow-left me-1" /> Back
        </Link>
      </div>

      {info === null ? (
        <div className="d-flex justify-content-center py-5">
          <div className="spinner-border text-primary" role="status">
            <span className="visually-hidden">Loading...</span>
          </div>
        </div>
      ) : (
        <>
          <div className="row g-4 mb-4">
            <div className="col-md-6">
              <div className="card border-0 shadow-sm h-100">
                <div className="card-body p-4">
                  <h6 className="fw-semibold mb-3">Your referral link</h6>
                  <div className="input-group">
                    <input type="text" className="form-control font-monospace small" readOnly value={referralLink} onClick={(e) => e.currentTarget.select()} />
                    <button type="button" className="btn btn-outline-secondary" onClick={copyLink}>
                      {copied ? "Copied!" : "Copy"}
                    </button>
                  </div>
                  <p className="text-muted small mt-2 mb-0">
                    Your code: <span className="font-monospace">{info.referralCode}</span>
                  </p>
                </div>
              </div>
            </div>
            <div className="col-md-6">
              <div className="card border-0 shadow-sm h-100">
                <div className="card-body p-4">
                  <h6 className="fw-semibold mb-1">Store credit balance</h6>
                  <div className="display-6 fw-bold text-success mb-1">{formatCurrency(info.creditBalance)}</div>
                  <p className="text-muted small mb-0">Applied automatically at your next checkout, up to your basket total.</p>
                </div>
              </div>
            </div>
          </div>

          <div className="card border-0 shadow-sm">
            <div className="card-header bg-transparent border-0 pt-4 px-4 pb-0">
              <h6 className="fw-semibold mb-0">Friends you&apos;ve invited</h6>
            </div>
            {info.referrals.length === 0 ? (
              <div className="text-center py-5">
                <div className="em-empty-icon">
                  <i className="bi bi-people" />
                </div>
                <p className="text-muted mb-0">No invites yet — share your link above to get started.</p>
              </div>
            ) : (
              <div className="table-responsive">
                <table className="table table-hover mb-0 align-middle">
                  <thead>
                    <tr className="border-bottom">
                      <th className="px-4 py-3 text-uppercase text-muted small fw-semibold">Friend</th>
                      <th className="py-3 text-uppercase text-muted small fw-semibold">Joined</th>
                      <th className="py-3 text-uppercase text-muted small fw-semibold">Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {info.referrals.map((referral, i) => (
                      <tr key={i}>
                        <td className="px-4 fw-semibold">{referral.friendName}</td>
                        <td className="text-muted small">{new Date(referral.invitedAt).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}</td>
                        <td>
                          {referral.rewarded ? (
                            <span className="badge rounded-pill text-bg-success">Reward earned</span>
                          ) : (
                            <span className="badge rounded-pill text-bg-secondary">Waiting on first order</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}

export default Referrals;
