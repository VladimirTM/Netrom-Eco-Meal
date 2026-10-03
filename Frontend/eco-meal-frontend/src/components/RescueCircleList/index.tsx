import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { rescueCirclesApi } from "../../api/clients/RescueCirclesApiClient";
import type { RescueCircleSummary } from "../../api/models/RescueCircle";
import { formatCurrency } from "../../utils/currency";

function statusLabel(circle: RescueCircleSummary): string {
  if (circle.status === "Cancelled") return "Cancelled";
  return circle.paidCount === circle.participantCount ? "Fully paid" : "Collecting payments";
}

function statusBadgeClass(circle: RescueCircleSummary): string {
  if (circle.status === "Cancelled") return "bg-secondary-subtle text-secondary";
  return circle.paidCount === circle.participantCount ? "bg-success-subtle text-success" : "bg-warning-subtle text-warning";
}

// Ports RescueCircleList.razor (/circles) — baskets being split with friends, as organizer or joiner.
function RescueCircleList() {
  const [circles, setCircles] = useState<RescueCircleSummary[] | null>(null);

  useEffect(() => {
    (async () => setCircles(await rescueCirclesApi.getMine()))();
  }, []);

  return (
    <>
      <section className="orders-hero">
        <div className="orders-hero-inner">
          <span className="orders-hero-eyebrow">
            <i className="bi bi-people" /> Group orders
          </span>
          <h1 className="orders-hero-title">Your Rescue Circles</h1>
          <p className="orders-hero-sub">Baskets you&apos;re splitting with friends — as organizer or as a joiner.</p>
        </div>
      </section>

      <section className="orders-list-section">
        {circles === null ? (
          <div className="d-flex justify-content-center py-5">
            <div className="spinner-border text-primary" role="status">
              <span className="visually-hidden">Loading...</span>
            </div>
          </div>
        ) : circles.length === 0 ? (
          <div className="orders-empty">
            <div className="em-empty-icon">
              <i className="bi bi-people" />
            </div>
            <p className="mb-1">No Rescue Circles yet.</p>
            <span className="text-muted small d-block mb-3">Start one from your basket to split an order with friends.</span>
            <Link to="/" className="btn btn-primary px-4">
              Browse packages
            </Link>
          </div>
        ) : (
          <div className="orders-stack">
            {circles.map((circle) => (
              <Link to={`/circles/${circle.id}`} className="order-ticket order-ticket-clickable text-decoration-none text-reset d-block" key={circle.id}>
                <div className="order-ticket-main">
                  <div className="order-ticket-top">
                    <div className="order-ticket-kitchen">
                      <div className="order-ticket-business">
                        <i className="bi bi-shop" />
                        {circle.businessName}
                      </div>
                    </div>
                    <span className={`badge ${statusBadgeClass(circle)}`}>{statusLabel(circle)}</span>
                  </div>
                  <div className="order-ticket-footer">
                    <div className="order-ticket-pickup">
                      <i className="bi bi-people" />
                      {circle.joinedCount} of {circle.participantCount} joined · {circle.paidCount} paid
                    </div>
                    <div className="order-ticket-total">
                      <span>Total</span>
                      <span className="order-ticket-total-value">{formatCurrency(circle.totalAmount)}</span>
                    </div>
                  </div>
                </div>
              </Link>
            ))}
          </div>
        )}
      </section>
    </>
  );
}

export default RescueCircleList;
