import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../../api/base/http";
import { businessesApi, type StaffMemberDto } from "../../api/clients/BusinessesApiClient";
import { businessTypesApi } from "../../api/clients/BusinessTypesApiClient";
import { usersApi, type UserWithRoleDto } from "../../api/clients/UsersApiClient";
import type { BusinessDto } from "../../api/models/Business";
import type { BusinessTypeDto } from "../../api/models/Lookup";
import type { PaginatedList } from "../../api/models/Pagination";
import { useAuth } from "../../context/AuthContext/auth-context";
import { useManagedBusiness } from "../../context/ManagedBusinessContext/managed-business-context";
import AnchoredDropdown from "../common/AnchoredDropdown";
import ConfirmDialog from "../common/ConfirmDialog";
import Pagination from "../common/Pagination";
import ReportDialog from "../common/ReportDialog";

const PAGE_SIZE = 10;

type ReasonPromptMode = "Reject" | "Hide";

function statusLabel(business: BusinessDto): string {
  if (business.status === "PendingApproval") return "Pending";
  if (business.status === "Rejected") return "Rejected";
  if (business.isHidden) return "Hidden";
  return "Approved";
}

function statusBadgeClass(business: BusinessDto): string {
  if (business.status === "PendingApproval") return "role-badge role-badge-businessmanager";
  if (business.status === "Rejected") return "bg-danger-subtle text-danger";
  if (business.isHidden) return "bg-secondary-subtle text-secondary";
  return "bg-success-subtle text-success";
}

function statusTitle(business: BusinessDto): string | undefined {
  if (business.status === "Rejected" && business.rejectionReason) return business.rejectionReason;
  if (business.isHidden && business.hiddenReason) return business.hiddenReason;
  return undefined;
}

// Ports Businesses.razor (/businesses): a BusinessManager's own businesses (read-only,
// via ManagedBusinessContext), or, for an Admin, the full platform list
// with search/type/status filters plus every moderation action (approve/reject/hide/unhide/
// delete, the staff-assignment dropdown, "Add Business").
function Businesses() {
  const { user } = useAuth();
  const isAdmin = user?.role === "Admin";
  const { myBusinesses, loading: myBusinessesLoading } = useManagedBusiness();

  const [paged, setPaged] = useState<PaginatedList<BusinessDto> | null>(null);
  const [businessTypes, setBusinessTypes] = useState<BusinessTypeDto[]>([]);
  const [businessManagers, setBusinessManagers] = useState<UserWithRoleDto[]>([]);
  const [staffByBusiness, setStaffByBusiness] = useState<Record<string, StaffMemberDto[]>>({});
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [pageIndex, setPageIndex] = useState(1);

  const [openStaffDropdownBusinessId, setOpenStaffDropdownBusinessId] = useState<string | null>(null);
  const [assignError, setAssignError] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = useState<BusinessDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [reasonPromptBusiness, setReasonPromptBusiness] = useState<BusinessDto | null>(null);
  const [reasonPromptMode, setReasonPromptMode] = useState<ReasonPromptMode | null>(null);
  const [reasonPromptBusy, setReasonPromptBusy] = useState(false);
  const [reasonPromptError, setReasonPromptError] = useState<string | null>(null);

  useEffect(() => {
    if (!isAdmin) return;
    businessTypesApi.getAll().then(setBusinessTypes);
    usersApi.getByRole("BusinessManager").then(setBusinessManagers);
  }, [isAdmin]);

  // Mirrors the Blazor page's own Debouncer — search reloads 300ms after the last keystroke, the
  // type/status filters reload immediately (see onTypeFilterChange/onStatusFilterChange).
  useEffect(() => {
    const handle = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(handle);
  }, [search]);

  const loadPage = useCallback(async () => {
    if (!isAdmin) return;
    const page = await businessesApi.getPaged({
      pageIndex,
      pageSize: PAGE_SIZE,
      search: debouncedSearch,
      businessTypeId: typeFilter || undefined,
      statusFilter: statusFilter || undefined,
    });
    // A mutation (e.g. deleting the last item on the last page) can shrink totalPages below the
    // page we're currently viewing — re-fetch the now-last valid page instead of showing empty.
    if (page.items.length === 0 && pageIndex > page.totalPages && page.totalPages > 0) {
      setPageIndex(page.totalPages);
      return;
    }
    setPaged(page);
    const staffEntries = await Promise.all(page.items.map(async (b) => [b.id, await businessesApi.getStaff(b.id)] as const));
    setStaffByBusiness(Object.fromEntries(staffEntries));
  }, [isAdmin, pageIndex, debouncedSearch, typeFilter, statusFilter]);

  useEffect(() => {
    // Synchronizes with the server whenever paging, search or the filters change (admin only —
    // a BusinessManager's own list comes straight from ManagedBusinessContext instead).
    // oxlint-disable-next-line react/set-state-in-effect
    loadPage();
  }, [loadPage]);

  function onSearchChange(value: string) {
    setSearch(value);
    setPageIndex(1);
  }

  function onTypeFilterChange(value: string) {
    setTypeFilter(value);
    setPageIndex(1);
  }

  function onStatusFilterChange(value: string) {
    setStatusFilter(value);
    setPageIndex(1);
  }

  function clearFilters() {
    setSearch("");
    setTypeFilter("");
    setStatusFilter("");
    setPageIndex(1);
  }

  const hasFilters = search !== "" || typeFilter !== "" || statusFilter !== "";

  function toggleStaffDropdown(businessId: string) {
    setOpenStaffDropdownBusinessId((cur) => (cur === businessId ? null : businessId));
  }

  async function refreshStaff(businessId: string) {
    const staff = await businessesApi.getStaff(businessId);
    setStaffByBusiness((prev) => ({ ...prev, [businessId]: staff }));
  }

  // Deliberately doesn't close the dropdown — an admin can add several staff in one open.
  async function addStaff(business: BusinessDto, userId: string, userName: string) {
    setAssignError(null);
    try {
      await businessesApi.addStaff(business.id, userId, userName);
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        setAssignError("That staff assignment already exists. Please try again.");
      } else {
        throw err;
      }
    }
    await refreshStaff(business.id);
  }

  async function removeStaff(business: BusinessDto, userId: string) {
    setAssignError(null);
    await businessesApi.removeStaff(business.id, userId);
    await refreshStaff(business.id);
  }

  async function approve(business: BusinessDto) {
    await businessesApi.approve(business.id);
    await loadPage();
  }

  async function unhide(business: BusinessDto) {
    await businessesApi.unhide(business.id);
    await loadPage();
  }

  function openReasonPrompt(business: BusinessDto, mode: ReasonPromptMode) {
    setReasonPromptBusiness(business);
    setReasonPromptMode(mode);
    setReasonPromptError(null);
  }

  function closeReasonPrompt() {
    setReasonPromptBusiness(null);
    setReasonPromptError(null);
  }

  async function submitReasonPrompt(reason: string) {
    if (!reasonPromptBusiness || !reasonPromptMode) return;

    setReasonPromptBusy(true);
    setReasonPromptError(null);
    try {
      if (reasonPromptMode === "Reject") await businessesApi.reject(reasonPromptBusiness.id, reason);
      else await businessesApi.hide(reasonPromptBusiness.id, reason);
      await loadPage();
      setReasonPromptBusiness(null);
    } catch (err) {
      setReasonPromptError(err instanceof Error ? err.message : "Something went wrong.");
    } finally {
      setReasonPromptBusy(false);
    }
  }

  function requestDelete(business: BusinessDto) {
    setPendingDelete(business);
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    await businessesApi.delete(pendingDelete.id);
    setDeleting(false);
    setPendingDelete(null);
    await loadPage();
  }

  const reasonPromptTitle = !reasonPromptBusiness
    ? ""
    : reasonPromptMode === "Reject"
      ? `Reject "${reasonPromptBusiness.name}"?`
      : `Hide "${reasonPromptBusiness.name}"?`;

  const reasonPromptMessage =
    reasonPromptMode === "Reject"
      ? "The applicant will see this reason, and it stays visible as a tooltip on the Rejected badge."
      : "It'll disappear from the customer-facing storefront immediately, but stays intact for you to edit or unhide later.";

  const reasonPromptPlaceholder = reasonPromptMode === "Reject" ? "Why is this application being rejected?" : "Reason (shown if someone hovers the Hidden badge)";

  const reasonPromptConfirmLabel = reasonPromptMode === "Reject" ? "Reject application" : "Hide business";

  return (
    <div>
      <div className="d-flex justify-content-between align-items-start mb-4">
        <div>
          <h1 className="h3 fw-bold mb-1">Businesses</h1>
          <p className="text-muted small mb-0">{isAdmin ? "Every registered food business" : "The businesses you manage"}</p>
        </div>
        {isAdmin && (
          <Link to="/businesses/create" className="btn btn-primary px-4">
            <i className="bi bi-plus-lg me-1" /> Add Business
          </Link>
        )}
      </div>

      {isAdmin ? (
        <>
          {assignError && (
            <div className="alert alert-danger" role="alert">
              {assignError}
            </div>
          )}

          {paged && (
            <div className="d-flex gap-3 mb-3 flex-wrap">
              <div className="input-group" style={{ maxWidth: 320 }}>
                <span className="input-group-text border-end-0">
                  <i className="bi bi-search text-muted" />
                </span>
                <input type="text" className="form-control border-start-0 ps-0" placeholder="Search name, description, address…" value={search} onChange={(e) => onSearchChange(e.target.value)} />
              </div>
              <select className="form-select" style={{ maxWidth: 200 }} value={typeFilter} onChange={(e) => onTypeFilterChange(e.target.value)}>
                <option value="">All types</option>
                {businessTypes.map((t) => (
                  <option value={t.id} key={t.id}>
                    {t.name}
                  </option>
                ))}
              </select>
              <select className="form-select" style={{ maxWidth: 200 }} value={statusFilter} onChange={(e) => onStatusFilterChange(e.target.value)}>
                <option value="">All statuses</option>
                <option value="PendingApproval">Pending approval</option>
                <option value="Approved">Approved</option>
                <option value="Rejected">Rejected</option>
                <option value="Hidden">Hidden</option>
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
                  <i className="bi bi-building" />
                </div>
                <p className="text-muted mb-0">{hasFilters ? "No businesses match your filters." : "No businesses registered yet."}</p>
              </div>
            ) : (
              <>
                <div className="table-responsive">
                  <table className="table table-hover mb-0 align-middle">
                    <thead>
                      <tr className="border-bottom">
                        <th className="px-4 py-3 text-uppercase text-muted small fw-semibold">Name</th>
                        <th className="py-3 text-uppercase text-muted small fw-semibold">Description</th>
                        <th className="py-3 text-uppercase text-muted small fw-semibold">Address</th>
                        <th className="py-3 text-uppercase text-muted small fw-semibold">Type</th>
                        <th className="py-3 text-uppercase text-muted small fw-semibold">Status</th>
                        <th className="py-3 text-uppercase text-muted small fw-semibold">Staff</th>
                        <th className="py-3 text-uppercase text-muted small fw-semibold">Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {paged.items.map((business) => {
                        const staff = staffByBusiness[business.id] ?? [];
                        const assignedIds = new Set(staff.map((s) => s.id));
                        const availableManagers = businessManagers.filter((m) => !assignedIds.has(m.id));

                        return (
                          <tr key={business.id}>
                            <td className="px-4 fw-semibold">{business.name}</td>
                            <td className="text-muted">{business.description}</td>
                            <td>{business.address}</td>
                            <td>
                              <span className="business-type-badge">{business.businessTypeName}</span>
                            </td>
                            <td>
                              <span className={`badge rounded-pill px-3 py-2 ${statusBadgeClass(business)}`} title={statusTitle(business)}>
                                {statusLabel(business)}
                              </span>
                            </td>
                            <td className="position-relative">
                              <div className="d-flex flex-wrap align-items-center gap-1">
                                {[...staff]
                                  .sort((a, b) => a.name.localeCompare(b.name))
                                  .map((s) => (
                                    <span className="role-badge role-badge-businessmanager" key={s.id}>
                                      {s.name}
                                      <button type="button" className="btn-close" style={{ fontSize: "0.55rem" }} title="Remove staff" onClick={() => removeStaff(business, s.id)} />
                                    </span>
                                  ))}
                                {business.status === "Approved" && (
                                  <AnchoredDropdown
                                    triggerClass="role-badge role-badge-unassigned"
                                    title="Add staff"
                                    isOpen={openStaffDropdownBusinessId === business.id}
                                    onToggle={() => toggleStaffDropdown(business.id)}
                                    onClose={() => setOpenStaffDropdownBusinessId(null)}
                                    trigger={<i className="bi bi-plus-lg" />}
                                  >
                                    {availableManagers.length === 0 ? (
                                      <div className="role-option text-muted">No more managers to add</div>
                                    ) : (
                                      availableManagers.map((manager) => (
                                        <button type="button" className="role-option" key={manager.id} onClick={() => addStaff(business, manager.id, manager.name)}>
                                          <span className="role-option-dot" style={{ backgroundColor: "#2563eb" }} />
                                          {manager.name}
                                        </button>
                                      ))
                                    )}
                                  </AnchoredDropdown>
                                )}
                              </div>
                            </td>
                            <td>
                              <div className="d-flex gap-2 flex-wrap">
                                {business.status === "PendingApproval" && (
                                  <>
                                    <button className="btn btn-sm btn-outline-success" title="Approve" onClick={() => approve(business)}>
                                      <i className="bi bi-check-lg" />
                                    </button>
                                    <button className="btn btn-sm btn-outline-danger" title="Reject" onClick={() => openReasonPrompt(business, "Reject")}>
                                      <i className="bi bi-x-lg" />
                                    </button>
                                  </>
                                )}
                                {business.status === "Rejected" && (
                                  <button className="btn btn-sm btn-outline-success" title="Reconsider — approve after all" onClick={() => approve(business)}>
                                    <i className="bi bi-arrow-counterclockwise" />
                                  </button>
                                )}
                                {business.status === "Approved" && (
                                  <Link to={`/businesses/edit/${business.id}`} className="btn btn-sm btn-outline-primary" title="Edit">
                                    <i className="bi bi-pencil" />
                                  </Link>
                                )}
                                {business.status === "Approved" &&
                                  (business.isHidden ? (
                                    <button className="btn btn-sm btn-outline-secondary" title="Unhide" onClick={() => unhide(business)}>
                                      <i className="bi bi-eye" />
                                    </button>
                                  ) : (
                                    <button className="btn btn-sm btn-outline-secondary" title="Hide from storefront" onClick={() => openReasonPrompt(business, "Hide")}>
                                      <i className="bi bi-eye-slash" />
                                    </button>
                                  ))}
                                <button className="btn btn-sm btn-outline-danger" title="Delete" onClick={() => requestDelete(business)}>
                                  <i className="bi bi-trash" />
                                </button>
                              </div>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
                <Pagination currentPage={paged.pageIndex} totalPages={paged.totalPages} onPageChange={setPageIndex} />
              </>
            )}
          </div>
        </>
      ) : (
        <div className="card border-0 shadow-sm">
          {myBusinessesLoading ? (
            <div className="d-flex justify-content-center py-5">
              <div className="spinner-border text-primary" role="status">
                <span className="visually-hidden">Loading...</span>
              </div>
            </div>
          ) : myBusinesses.length === 0 ? (
            <div className="text-center py-5">
              <div className="em-empty-icon">
                <i className="bi bi-building" />
              </div>
              <p className="text-muted mb-0">You're not assigned to manage any business yet. Ask an admin to add you as staff.</p>
            </div>
          ) : (
            <div className="table-responsive">
              <table className="table table-hover mb-0 align-middle">
                <thead>
                  <tr className="border-bottom">
                    <th className="px-4 py-3 text-uppercase text-muted small fw-semibold">Name</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Description</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Address</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Type</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {myBusinesses.map((business) => (
                    <tr key={business.id}>
                      <td className="px-4 fw-semibold">{business.name}</td>
                      <td className="text-muted">{business.description}</td>
                      <td>{business.address}</td>
                      <td>
                        <span className="business-type-badge">{business.businessTypeName}</span>
                      </td>
                      <td>
                        <Link to={`/businesses/edit/${business.id}`} className="btn btn-sm btn-outline-primary" title="Edit">
                          <i className="bi bi-pencil" />
                        </Link>
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
        title="Delete business?"
        message={pendingDelete === null ? "" : `"${pendingDelete.name}", every package it lists, and its full order history will be permanently removed. This can't be undone.`}
        confirmLabel="Delete business"
        busy={deleting}
        onConfirm={confirmDelete}
        onCancel={() => setPendingDelete(null)}
      />

      <ReportDialog
        isOpen={reasonPromptBusiness !== null}
        targetLabel={reasonPromptBusiness?.name ?? ""}
        title={reasonPromptTitle}
        message={reasonPromptMessage}
        placeholder={reasonPromptPlaceholder}
        confirmLabel={reasonPromptConfirmLabel}
        busy={reasonPromptBusy}
        error={reasonPromptError}
        onSubmit={submitReasonPrompt}
        onCancel={closeReasonPrompt}
      />
    </div>
  );
}

export default Businesses;
