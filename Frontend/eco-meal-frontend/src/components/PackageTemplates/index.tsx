import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { businessesApi } from "../../api/clients/BusinessesApiClient";
import { packageTemplatesApi } from "../../api/clients/PackageTemplatesApiClient";
import type { PackageTemplateDto } from "../../api/models/PackageTemplate";
import { useAuth } from "../../context/AuthContext/auth-context";
import { useManagedBusiness } from "../../context/ManagedBusinessContext/managed-business-context";
import ConfirmDialog from "../common/ConfirmDialog";
import ForbiddenPanel from "../common/ForbiddenPanel";
import LoadingSpinner from "../common/LoadingSpinner";

function pad2(n: number): string {
  return String(n).padStart(2, "0");
}

// A daily time-of-day stored in UTC ("HH:mm:ss") -> its local clock-time label, by building the
// matching UTC instant for today's date and reading it back with the Date object's local getters —
// same reasoning as utils/dateTimeLocal.ts, mirrors ClientTimeZoneService.ToLocal(today + timeOfDay).
function toLocalTimeLabel(timeOfDayUtc: string): string {
  const [hours, minutes] = timeOfDayUtc.split(":").map(Number);
  const today = new Date();
  const utcInstant = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate(), hours, minutes));
  return `${pad2(utcInstant.getHours())}:${pad2(utcInstant.getMinutes())}`;
}

function windowLabel(template: PackageTemplateDto): string {
  return `${toLocalTimeLabel(template.pickupStartTimeUtc)} – ${toLocalTimeLabel(template.pickupEndTimeUtc)}`;
}

// DateOnly ("yyyy-MM-dd") — no UTC instant to convert, same reasoning as utils/dates.ts's own
// formatDateOnly, just without the year (mirrors the Razor page's "MMM d" format).
function lastGeneratedLabel(dateOnly: string | null): string {
  if (!dateOnly) return "—";
  const [year, month, day] = dateOnly.split("-").map(Number);
  return new Intl.DateTimeFormat("en-US", { month: "short", day: "numeric" }).format(new Date(year, month - 1, day));
}

// Ports PackageTemplates.razor (/packages/templates). Admin sees every business's templates (with
// its own Business column); a BusinessManager is scoped to whichever business is currently
// selected in the sidebar switcher.
function PackageTemplates() {
  const { user } = useAuth();
  const isAdmin = user?.role === "Admin";
  const { selectedBusinessId: myBusinessId, loading: managedBusinessLoading } = useManagedBusiness();

  const [templates, setTemplates] = useState<PackageTemplateDto[] | null>(null);
  const [businessNames, setBusinessNames] = useState<Record<string, string>>({});
  const [pendingDelete, setPendingDelete] = useState<PackageTemplateDto | null>(null);
  const [deleting, setDeleting] = useState(false);

  const forbidden = !isAdmin && !managedBusinessLoading && myBusinessId === null;

  const loadTemplates = useCallback(async () => {
    const list = isAdmin ? await packageTemplatesApi.getAll() : myBusinessId ? await packageTemplatesApi.getByBusiness(myBusinessId) : [];
    setTemplates(list);
  }, [isAdmin, myBusinessId]);

  useEffect(() => {
    if (!isAdmin && managedBusinessLoading) return;
    if (forbidden) return;
    // Synchronizes with the server on mount and whenever the managed business selection changes.
    // oxlint-disable-next-line react/set-state-in-effect
    loadTemplates();
  }, [loadTemplates, isAdmin, managedBusinessLoading, forbidden]);

  useEffect(() => {
    if (isAdmin) {
      businessesApi.getAll(false).then((list) => {
        setBusinessNames(Object.fromEntries(list.map((b) => [b.id, b.name])));
      });
    }
  }, [isAdmin]);

  async function toggleActive(template: PackageTemplateDto) {
    await packageTemplatesApi.setActive(template.id, !template.isActive);
    await loadTemplates();
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await packageTemplatesApi.remove(pendingDelete.id);
      await loadTemplates();
    } finally {
      setDeleting(false);
      setPendingDelete(null);
    }
  }

  return (
    <div>
      <div className="d-flex justify-content-between align-items-start mb-4">
        <div>
          <h1 className="h3 fw-bold mb-1">Recurring Templates</h1>
          <p className="text-muted small mb-0">Packages tagged "Repeat this every day" auto-generate a fresh instance every day at the same pickup window.</p>
        </div>
        <Link to="/packages" className="btn btn-outline-secondary">
          <i className="bi bi-arrow-left me-1" /> Back
        </Link>
      </div>

      {!isAdmin && managedBusinessLoading ? (
        <LoadingSpinner />
      ) : forbidden ? (
        <ForbiddenPanel message="You don't manage a business yet, so you have no recurring templates." backHref="/packages" backLabel="Back to packages" />
      ) : (
        <div className="card border-0 shadow-sm">
          {templates === null ? (
            <div className="d-flex justify-content-center py-5">
              <div className="spinner-border text-primary" role="status">
                <span className="visually-hidden">Loading...</span>
              </div>
            </div>
          ) : templates.length === 0 ? (
            <div className="text-center py-5">
              <div className="em-empty-icon">
                <i className="bi bi-arrow-repeat" />
              </div>
              <p className="text-muted mb-3">No recurring templates yet.</p>
              <p className="text-muted small mb-0">Tick "Repeat this every day" while creating a package to turn it into one.</p>
            </div>
          ) : (
            <div className="table-responsive">
              <table className="table table-hover mb-0 align-middle">
                <thead>
                  <tr className="border-bottom">
                    <th className="px-4 py-3 text-uppercase text-muted small fw-semibold">Name</th>
                    {isAdmin && <th className="py-3 text-uppercase text-muted small fw-semibold">Business</th>}
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Daily window</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Qty/day</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Last generated</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Status</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {templates.map((template) => (
                    <tr key={template.id}>
                      <td className="px-4">
                        <div className="fw-semibold">{template.name}</div>
                        <div className="text-muted small">{template.description}</div>
                      </td>
                      {isAdmin && <td>{businessNames[template.businessId] ?? "—"}</td>}
                      <td className="text-muted small">{windowLabel(template)}</td>
                      <td>{template.quantity}</td>
                      <td className="text-muted small">{lastGeneratedLabel(template.lastGeneratedDate)}</td>
                      <td>
                        {template.isActive ? (
                          <span className="badge rounded-pill text-bg-success">Active</span>
                        ) : (
                          <span className="badge rounded-pill text-bg-secondary">Paused</span>
                        )}
                      </td>
                      <td>
                        <div className="d-flex gap-2">
                          <button className="btn btn-sm btn-outline-secondary" onClick={() => toggleActive(template)}>
                            {template.isActive ? "Pause" : "Resume"}
                          </button>
                          <button className="btn btn-sm btn-outline-danger" title="Delete" onClick={() => setPendingDelete(template)}>
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
      )}

      <ConfirmDialog
        isOpen={pendingDelete !== null}
        title="Stop repeating?"
        message={pendingDelete ? `"${pendingDelete.name}" will stop generating new daily packages. Instances already created stay as they are.` : ""}
        confirmLabel="Stop repeating"
        busy={deleting}
        onConfirm={confirmDelete}
        onCancel={() => setPendingDelete(null)}
      />
    </div>
  );
}

export default PackageTemplates;
