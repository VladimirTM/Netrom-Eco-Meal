import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { standingOrdersApi } from "../../api/clients/StandingOrdersApiClient";
import type { StandingOrderDto } from "../../api/models/StandingOrder";
import ConfirmDialog from "../common/ConfirmDialog";
import { formatCurrency } from "../../utils/currency";

// Ports StandingOrders.razor (/standing-orders) — lists/manages existing "usuals"; creation
// happens from BusinessDetail's own "Standing order" section, not here.
function StandingOrders() {
  const [standingOrders, setStandingOrders] = useState<StandingOrderDto[] | null>(null);
  const [pendingDelete, setPendingDelete] = useState<StandingOrderDto | null>(null);
  const [deleting, setDeleting] = useState(false);

  async function load() {
    setStandingOrders(await standingOrdersApi.getMine());
  }

  useEffect(() => {
    (async () => load())();
  }, []);

  async function toggleActive(standing: StandingOrderDto) {
    await standingOrdersApi.update(standing.id, standing.maxWeeklySpend, !standing.isActive);
    await load();
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    await standingOrdersApi.remove(pendingDelete.id);
    setStandingOrders((prev) => prev?.filter((s) => s.id !== pendingDelete.id) ?? null);
    setDeleting(false);
    setPendingDelete(null);
  }

  return (
    <div className="container py-4">
      <div className="d-flex justify-content-between align-items-start mb-4">
        <div>
          <h1 className="h3 fw-bold mb-1">Standing Orders</h1>
          <p className="text-muted small mb-0">
            Your saved &quot;usuals&quot; — set one up from a kitchen&apos;s page, and we&apos;ll reserve it in your basket the moment it goes live.
          </p>
        </div>
        <Link to="/" className="btn btn-outline-secondary">
          <i className="bi bi-arrow-left me-1" /> Back
        </Link>
      </div>

      <div className="card border-0 shadow-sm">
        {standingOrders === null ? (
          <div className="d-flex justify-content-center py-5">
            <div className="spinner-border text-primary" role="status">
              <span className="visually-hidden">Loading...</span>
            </div>
          </div>
        ) : standingOrders.length === 0 ? (
          <div className="text-center py-5">
            <div className="em-empty-icon">
              <i className="bi bi-arrow-repeat" />
            </div>
            <p className="text-muted mb-3">No standing orders yet.</p>
            <p className="text-muted small mb-0">Open a kitchen you rescue from often and look for the &quot;Standing order&quot; section on its page.</p>
          </div>
        ) : (
          <div className="table-responsive">
            <table className="table table-hover mb-0 align-middle">
              <thead>
                <tr className="border-bottom">
                  <th className="px-4 py-3 text-uppercase text-muted small fw-semibold">Kitchen</th>
                  <th className="py-3 text-uppercase text-muted small fw-semibold">Matches</th>
                  <th className="py-3 text-uppercase text-muted small fw-semibold">Weekly budget</th>
                  <th className="py-3 text-uppercase text-muted small fw-semibold">Status</th>
                  <th className="py-3 text-uppercase text-muted small fw-semibold">Actions</th>
                </tr>
              </thead>
              <tbody>
                {standingOrders.map((standing) => (
                  <tr key={standing.id}>
                    <td className="px-4">
                      <Link to={`/businesses/${standing.businessId}`} className="fw-semibold text-decoration-none">
                        {standing.businessName}
                      </Link>
                    </td>
                    <td className="text-muted small">
                      {standing.packageTypeName ?? "Any package"}
                      {standing.dietaryTag ? ` · ${standing.dietaryTag}` : ""}
                    </td>
                    <td>{formatCurrency(standing.maxWeeklySpend)}/week</td>
                    <td>
                      {standing.isActive ? (
                        <span className="badge rounded-pill text-bg-success">Active</span>
                      ) : (
                        <span className="badge rounded-pill text-bg-secondary">Paused</span>
                      )}
                    </td>
                    <td>
                      <div className="d-flex gap-2">
                        <button className="btn btn-sm btn-outline-secondary" onClick={() => void toggleActive(standing)}>
                          {standing.isActive ? "Pause" : "Resume"}
                        </button>
                        <button className="btn btn-sm btn-outline-danger" title="Remove" onClick={() => setPendingDelete(standing)}>
                          <i className="bi bi-trash" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <ConfirmDialog
        isOpen={pendingDelete !== null}
        title="Remove this standing order?"
        message={pendingDelete ? `You'll stop being notified when "${pendingDelete.packageTypeName ?? "your usual"}" from ${pendingDelete.businessName} goes live.` : ""}
        confirmLabel="Remove"
        busy={deleting}
        onConfirm={() => void confirmDelete()}
        onCancel={() => setPendingDelete(null)}
      />
    </div>
  );
}

export default StandingOrders;
