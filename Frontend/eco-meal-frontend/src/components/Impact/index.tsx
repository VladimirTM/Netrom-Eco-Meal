import { useEffect, useState } from "react";
import { impactApi } from "../../api/clients/ImpactApiClient";
import { ordersApi } from "../../api/clients/OrdersApiClient";
import type { LeaderboardEntry } from "../../api/models/Impact";
import EmptyState from "../common/EmptyState";
import { useAuth } from "../../context/AuthContext/auth-context";
import { kmNotDriven, litersOfWater } from "../../utils/impactEquivalency";
import { getInitial } from "../../utils/textHelpers";

function rankClass(index: number): string {
  return index === 0 ? "impact-rank-gold" : index === 1 ? "impact-rank-silver" : index === 2 ? "impact-rank-bronze" : "";
}

function Impact() {
  const { user } = useAuth();
  const isCustomer = user?.role === "Customer";

  const [leaderboard, setLeaderboard] = useState<LeaderboardEntry[] | null>(null);
  const [optedIn, setOptedIn] = useState(false);
  const [optInBusy, setOptInBusy] = useState(false);
  const [totalKgSaved, setTotalKgSaved] = useState<number | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      const [board, kgSaved, myOptIn] = await Promise.all([
        impactApi.getMonthlyLeaderboard(),
        ordersApi.getTotalKgSaved(),
        isCustomer ? impactApi.getMyOptInStatus() : Promise.resolve(false),
      ]);
      if (cancelled) return;
      setLeaderboard(board);
      setTotalKgSaved(kgSaved);
      setOptedIn(myOptIn);
    })();
    return () => {
      cancelled = true;
    };
  }, [isCustomer]);

  async function toggleOptIn(checked: boolean) {
    setOptInBusy(true);
    await impactApi.setMyOptInStatus(checked);
    setOptedIn(checked);
    setOptInBusy(false);
    // Opting in/out changes whether the viewer's own row should now appear/disappear.
    setLeaderboard(await impactApi.getMonthlyLeaderboard());
  }

  return (
    <>
      <section className="impact-hero">
        <div className="impact-hero-inner">
          <span className="home-hero-eyebrow">
            <i className="bi bi-trophy-fill" /> This month&apos;s top rescuers
          </span>
          <h1 className="home-hero-title">
            Community <span className="home-hero-highlight">Impact</span>
          </h1>
          <p className="home-hero-sub">
            Every completed pickup keeps real food out of the bin. This board ranks the customers who&apos;ve saved the
            most this month — opt in below to add your own name to it.
          </p>
          {totalKgSaved !== null && totalKgSaved > 0 && (
            <>
              <p className="home-hero-sub mb-0">
                <strong>{totalKgSaved.toFixed(1).replace(/\.0$/, "")} kg</strong> saved platform-wide so far.
              </p>
              <div className="impact-equivalency">
                <span className="impact-equivalency-item">
                  <i className="bi bi-car-front" /> ~{Math.round(kmNotDriven(totalKgSaved))} km not driven
                </span>
                <span className="impact-equivalency-item">
                  <i className="bi bi-droplet" /> ~{Math.round(litersOfWater(totalKgSaved)).toLocaleString()} L of water saved
                </span>
              </div>
            </>
          )}
        </div>
      </section>

      <section className="impact-body">
        {isCustomer && (
          <div className="impact-optin-card">
            <div>
              <div className="fw-semibold">Appear on this leaderboard</div>
              <div className="text-muted small">Off by default — your name only shows once you opt in.</div>
            </div>
            <div className="form-check form-switch">
              <input
                className="form-check-input"
                type="checkbox"
                role="switch"
                id="leaderboard-optin"
                checked={optedIn}
                disabled={optInBusy}
                onChange={(e) => void toggleOptIn(e.target.checked)}
              />
              <label className="form-check-label small" htmlFor="leaderboard-optin">
                {optedIn ? "Visible" : "Hidden"}
              </label>
            </div>
          </div>
        )}

        {leaderboard === null ? (
          <div className="d-flex justify-content-center py-5">
            <div className="spinner-border text-primary" role="status">
              <span className="visually-hidden">Loading...</span>
            </div>
          </div>
        ) : leaderboard.length === 0 ? (
          <EmptyState icon="bi-trophy" message="No one has opted in yet this month — be the first rescuer on the board." />
        ) : (
          <ol className="impact-list">
            {leaderboard.map((entry, i) => {
              const isMe = entry.userId === user?.id;
              return (
                <li className={`impact-row ${isMe ? "impact-row-me" : ""}`} key={entry.userId}>
                  <span className={`impact-rank ${rankClass(i)}`}>{i + 1}</span>
                  <span className="impact-avatar">{getInitial(entry.displayName)}</span>
                  <span className="impact-name">
                    {entry.displayName}
                    {isMe && <span className="badge rounded-pill em-badge-neutral border small fw-normal ms-1">You</span>}
                    {entry.streakWeeks > 0 && (
                      <span
                        className="badge rounded-pill bg-warning-subtle text-warning-emphasis border border-warning-subtle small fw-normal ms-1"
                        title="Consecutive weeks with a completed rescue"
                      >
                        <i className="bi bi-fire" /> {entry.streakWeeks}
                      </span>
                    )}
                  </span>
                  <span className="impact-kg">{entry.kgSaved.toFixed(1).replace(/\.0$/, "")} kg</span>
                </li>
              );
            })}
          </ol>
        )}
      </section>
    </>
  );
}

export default Impact;
