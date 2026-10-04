import { useCallback, useEffect, useState } from "react";
import { auditLogApi } from "../../api/clients/AuditLogApiClient";
import type { AuditLogDto } from "../../api/models/AuditLog";
import type { PaginatedList } from "../../api/models/Pagination";
import Pagination from "../common/Pagination";

const PAGE_SIZE = 20;

// Mirrors AuditLog.razor's own curated subset — not the full AuditActions enum (31 constants);
// this is deliberately the same smaller dropdown, not a shortcut.
const ACTIONS = [
  "RoleChanged",
  "BusinessCreated",
  "BusinessUpdated",
  "BusinessDeleted",
  "BusinessStaffAdded",
  "BusinessStaffRemoved",
  "BusinessApplied",
  "BusinessApproved",
  "BusinessRejected",
  "BusinessHidden",
  "BusinessUnhidden",
  "PackageHidden",
  "PackageUnhidden",
  "ReportDismissed",
  "ReportActionTaken",
];

// Splits "BusinessStaffAdded" -> "Business Staff Added" for display.
function displayAction(action: string): string {
  return action.replace(/(?!^)([A-Z])/g, " $1");
}

// Ports AuditLog.razor (/audit-log): a read-only, searchable/filterable log of who did what.
function AuditLog() {
  const [paged, setPaged] = useState<PaginatedList<AuditLogDto> | null>(null);
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [actionFilter, setActionFilter] = useState("");
  const [targetTypeFilter, setTargetTypeFilter] = useState("");
  const [pageIndex, setPageIndex] = useState(1);

  useEffect(() => {
    const handle = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(handle);
  }, [search]);

  const loadPage = useCallback(async () => {
    const page = await auditLogApi.getPaged(pageIndex, PAGE_SIZE, actionFilter || undefined, targetTypeFilter || undefined, debouncedSearch || undefined);
    setPaged(page);
  }, [pageIndex, actionFilter, targetTypeFilter, debouncedSearch]);

  useEffect(() => {
    // oxlint-disable-next-line react/set-state-in-effect
    loadPage();
  }, [loadPage]);

  function onSearchChange(value: string) {
    setSearch(value);
    setPageIndex(1);
  }

  function onActionFilterChange(value: string) {
    setActionFilter(value);
    setPageIndex(1);
  }

  function onTargetTypeFilterChange(value: string) {
    setTargetTypeFilter(value);
    setPageIndex(1);
  }

  function clearFilters() {
    setSearch("");
    setActionFilter("");
    setTargetTypeFilter("");
    setPageIndex(1);
  }

  const hasFilters = search !== "" || actionFilter !== "" || targetTypeFilter !== "";

  return (
    <div>
      <div className="d-flex justify-content-between align-items-start mb-4">
        <div>
          <h1 className="h3 fw-bold mb-1">Audit Log</h1>
          <p className="text-muted small mb-0">Who did what — role changes, business approvals, and moderation actions</p>
        </div>
      </div>

      {paged && (
        <div className="d-flex gap-3 mb-3 flex-wrap">
          <div className="input-group" style={{ maxWidth: 320 }}>
            <span className="input-group-text border-end-0">
              <i className="bi bi-search text-muted" />
            </span>
            <input type="text" className="form-control border-start-0 ps-0" placeholder="Search actor or target…" value={search} onChange={(e) => onSearchChange(e.target.value)} />
          </div>
          <select className="form-select" style={{ maxWidth: 220 }} value={actionFilter} onChange={(e) => onActionFilterChange(e.target.value)}>
            <option value="">All actions</option>
            {ACTIONS.map((action) => (
              <option value={action} key={action}>
                {displayAction(action)}
              </option>
            ))}
          </select>
          <select className="form-select" style={{ maxWidth: 180 }} value={targetTypeFilter} onChange={(e) => onTargetTypeFilterChange(e.target.value)}>
            <option value="">All targets</option>
            <option value="User">User</option>
            <option value="Business">Business</option>
            <option value="Package">Package</option>
          </select>
          {hasFilters && (
            <button className="btn btn-outline-secondary" onClick={clearFilters}>
              <i className="bi bi-x-lg me-1" /> Clear
            </button>
          )}
          <span className="align-self-center text-muted small ms-auto">
            {paged.totalCount} result{paged.totalCount === 1 ? "" : "s"}
          </span>
        </div>
      )}

      <div className="card border-0 shadow-sm">
        {paged === null ? (
          <div className="d-flex justify-content-center py-5">
            <div className="spinner-border text-primary" role="status">
              <span className="visually-hidden">Loading...</span>
            </div>
          </div>
        ) : paged.items.length === 0 ? (
          <div className="text-center py-5">
            <div className="em-empty-icon">
              <i className="bi bi-journal-text" />
            </div>
            <p className="text-muted mb-0">{paged.totalCount === 0 && !hasFilters ? "Nothing logged yet." : "No entries match your filters."}</p>
          </div>
        ) : (
          <>
            <div className="table-responsive">
              <table className="table table-hover mb-0 align-middle">
                <thead>
                  <tr className="border-bottom">
                    <th className="px-4 py-3 text-uppercase text-muted small fw-semibold">Date</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Actor</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Action</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Target</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Details</th>
                  </tr>
                </thead>
                <tbody>
                  {paged.items.map((entry) => (
                    <tr key={entry.id}>
                      <td className="px-4 text-muted small">
                        {new Date(entry.createdAt).toLocaleString("en-US", { month: "short", day: "numeric", year: "numeric", hour: "2-digit", minute: "2-digit", hour12: false })}
                      </td>
                      <td>{entry.actorName}</td>
                      <td>
                        <span className="badge rounded-pill px-2 py-1 em-badge-neutral border">{displayAction(entry.action)}</span>
                      </td>
                      <td>{entry.targetName}</td>
                      <td className="text-muted small">{entry.details}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination currentPage={paged.pageIndex} totalPages={paged.totalPages} onPageChange={setPageIndex} />
          </>
        )}
      </div>
    </div>
  );
}

export default AuditLog;
