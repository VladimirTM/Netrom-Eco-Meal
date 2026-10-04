import { useEffect, useState } from "react";
import { ApiError } from "../../api/base/http";
import { reportsApi } from "../../api/clients/ReportsApiClient";
import type { ReportViewDto } from "../../api/models/Report";
import ConfirmDialog from "../common/ConfirmDialog";

function targetTypeLabel(targetType: string): string {
  if (targetType === "Business") return "Business";
  if (targetType === "KitchenTip") return "Kitchen tip";
  return "Package";
}

// Ports Reports.razor (/reports): open reports from customers about businesses/packages/kitchen
// tips, each dismissible or actionable (hides the target, using the report's own reason).
function Reports() {
  const [reports, setReports] = useState<ReportViewDto[] | null>(null);
  const [pendingDismiss, setPendingDismiss] = useState<ReportViewDto | null>(null);
  const [pendingAction, setPendingAction] = useState<ReportViewDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setReports(await reportsApi.getOpen());
  }

  useEffect(() => {
    // oxlint-disable-next-line react/set-state-in-effect
    load();
  }, []);

  async function confirmDismiss() {
    if (!pendingDismiss) return;
    setBusy(true);
    setError(null);
    try {
      await reportsApi.dismiss(pendingDismiss.id);
    } catch (err) {
      if (err instanceof ApiError && err.status === 403) setError("You're no longer allowed to manage reports.");
      else throw err;
    }
    await load();
    setBusy(false);
    setPendingDismiss(null);
  }

  async function confirmTakeAction() {
    if (!pendingAction) return;
    setBusy(true);
    setError(null);
    try {
      await reportsApi.takeAction(pendingAction.id, pendingAction.reason);
    } catch (err) {
      if (err instanceof ApiError && err.status === 403) setError("You're no longer allowed to manage reports.");
      else throw err;
    }
    await load();
    setBusy(false);
    setPendingAction(null);
  }

  return (
    <div>
      <div className="d-flex justify-content-between align-items-start mb-4">
        <div>
          <h1 className="h3 fw-bold mb-1">Reports</h1>
          <p className="text-muted small mb-0">Open reports from customers about businesses and packages</p>
        </div>
      </div>

      {error && (
        <div className="alert alert-danger" role="alert">
          {error}
        </div>
      )}

      <div className="card border-0 shadow-sm">
        {reports === null ? (
          <div className="d-flex justify-content-center py-5">
            <div className="spinner-border text-primary" role="status">
              <span className="visually-hidden">Loading...</span>
            </div>
          </div>
        ) : reports.length === 0 ? (
          <div className="text-center py-5">
            <div className="em-empty-icon">
              <i className="bi bi-flag" />
            </div>
            <p className="text-muted mb-0">No open reports — nothing needs review right now.</p>
          </div>
        ) : (
          <div className="table-responsive">
            <table className="table table-hover mb-0 align-middle">
              <thead>
                <tr className="border-bottom">
                  <th className="px-4 py-3 text-uppercase text-muted small fw-semibold">Target</th>
                  <th className="py-3 text-uppercase text-muted small fw-semibold">Reason</th>
                  <th className="py-3 text-uppercase text-muted small fw-semibold">Reported by</th>
                  <th className="py-3 text-uppercase text-muted small fw-semibold">Date</th>
                  <th className="py-3 text-uppercase text-muted small fw-semibold">Actions</th>
                </tr>
              </thead>
              <tbody>
                {reports.map((view) => (
                  <tr key={view.id}>
                    <td className="px-4">
                      <div className="fw-semibold">{view.targetName}</div>
                      <span className="badge rounded-pill px-2 py-1 em-badge-neutral border">{targetTypeLabel(view.targetType)}</span>
                    </td>
                    <td className="text-muted">{view.reason}</td>
                    <td>{view.reporterName}</td>
                    <td className="text-muted small">{new Date(view.createdAt).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}</td>
                    <td>
                      <div className="d-flex gap-2">
                        <button className="btn btn-sm btn-outline-secondary" onClick={() => setPendingDismiss(view)}>
                          Dismiss
                        </button>
                        <button className="btn btn-sm btn-outline-danger" onClick={() => setPendingAction(view)}>
                          <i className="bi bi-eye-slash me-1" /> Hide target
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
        isOpen={pendingDismiss !== null}
        title="Dismiss this report?"
        message={pendingDismiss === null ? "" : `No action will be taken on "${pendingDismiss.targetName}". The report is closed.`}
        confirmLabel="Dismiss"
        confirmClass="btn-secondary"
        busy={busy}
        onConfirm={confirmDismiss}
        onCancel={() => setPendingDismiss(null)}
      />

      <ConfirmDialog
        isOpen={pendingAction !== null}
        title="Hide this target?"
        message={pendingAction === null ? "" : `"${pendingAction.targetName}" will be hidden from the storefront for: ${pendingAction.reason}`}
        confirmLabel="Hide"
        busy={busy}
        onConfirm={confirmTakeAction}
        onCancel={() => setPendingAction(null)}
      />
    </div>
  );
}

export default Reports;
