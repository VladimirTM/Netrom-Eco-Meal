import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../../api/base/http";
import { businessesApi } from "../../api/clients/BusinessesApiClient";
import { packageTypesApi } from "../../api/clients/PackageTypesApiClient";
import { packagesApi, type MarkdownSuggestionDto } from "../../api/clients/PackagesApiClient";
import type { BusinessDto } from "../../api/models/Business";
import type { PackageTypeDto } from "../../api/models/Lookup";
import type { PackageDto } from "../../api/models/Package";
import type { PaginatedList } from "../../api/models/Pagination";
import { useAuth } from "../../context/AuthContext/auth-context";
import { useManagedBusiness } from "../../context/ManagedBusinessContext/managed-business-context";
import { useTimeZone } from "../../context/TimeZoneContext/timezone-context";
import ConfirmDialog from "../common/ConfirmDialog";
import Pagination from "../common/Pagination";
import ReportDialog from "../common/ReportDialog";
import { formatCurrency } from "../../utils/currency";
import { formatPickupWindow } from "../../utils/packagePickup";

const PAGE_SIZE = 10;
// An unassigned manager has no business — passed as the businessId filter instead of leaving it
// undefined, which would otherwise show every business's packages. Mirrors Packages.razor's own
// "fall back to a Guid that can never match" comment.
const NEVER_MATCH_ID = "00000000-0000-0000-0000-000000000000";

type BulkMode = "none" | "adjustQuantity" | "extendPickup";

// Ports Packages.razor (/packages). Admin sees every business (with a business-filter dropdown);
// a BusinessManager is scoped to whichever business is currently selected in the sidebar switcher.
function Packages() {
  const { user } = useAuth();
  const timeZone = useTimeZone();
  const isAdmin = user?.role === "Admin";
  const isBusinessManager = user?.role === "BusinessManager";
  const { selectedBusinessId: myBusinessId, loading: managedBusinessLoading } = useManagedBusiness();

  const [paged, setPaged] = useState<PaginatedList<PackageDto> | null>(null);
  const [allBusinesses, setAllBusinesses] = useState<BusinessDto[]>([]);
  const [packageTypes, setPackageTypes] = useState<PackageTypeDto[]>([]);
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [businessFilter, setBusinessFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [pageIndex, setPageIndex] = useState(1);

  // Selection persists across pages/filters so a manager can build it up while paging, then act
  // on all of it at once — cleared only when the managed business itself changes (below).
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [resetForBusinessId, setResetForBusinessId] = useState(myBusinessId);
  if (myBusinessId !== resetForBusinessId) {
    setResetForBusinessId(myBusinessId);
    setPageIndex(1);
    setSelectedIds(new Set());
  }

  const [pendingDelete, setPendingDelete] = useState<PackageDto | null>(null);
  const [deleting, setDeleting] = useState(false);

  const [hidePromptPackage, setHidePromptPackage] = useState<PackageDto | null>(null);
  const [hidePromptBusy, setHidePromptBusy] = useState(false);
  const [hidePromptError, setHidePromptError] = useState<string | null>(null);

  const [donatePromptPackage, setDonatePromptPackage] = useState<PackageDto | null>(null);
  const [donatePromptBusy, setDonatePromptBusy] = useState(false);

  const [confirmingDuplicate, setConfirmingDuplicate] = useState(false);
  const [bulkMode, setBulkMode] = useState<BulkMode>("none");
  const [bulkQuantityDelta, setBulkQuantityDelta] = useState(0);
  const [bulkExtendHours, setBulkExtendHours] = useState(1);
  const [bulkBusy, setBulkBusy] = useState(false);
  const [bulkError, setBulkError] = useState<string | null>(null);

  // Candidate ids are fetched business-wide (not paginated) so the badge shows regardless of
  // which page a candidate lands on; the actual AI call only happens once a manager clicks it.
  const [markdownCandidateIds, setMarkdownCandidateIds] = useState<Set<string>>(new Set());
  const [markdownSuggestionPackage, setMarkdownSuggestionPackage] = useState<PackageDto | null>(null);
  const [markdownSuggestionLoading, setMarkdownSuggestionLoading] = useState(false);
  const [markdownSuggestion, setMarkdownSuggestion] = useState<MarkdownSuggestionDto | null>(null);
  const [markdownSuggestionError, setMarkdownSuggestionError] = useState<string | null>(null);
  const [markdownDismissBusy, setMarkdownDismissBusy] = useState(false);

  // Same "fetched business-wide, not paginated" reasoning as markdownCandidateIds above.
  const [donationCandidateIds, setDonationCandidateIds] = useState<Set<string>>(new Set());

  const effectiveBusinessId = isBusinessManager ? (myBusinessId ?? NEVER_MATCH_ID) : businessFilter || undefined;

  // Mirrors the Blazor page's own Debouncer — search reloads 300ms after the last keystroke, every
  // other filter reloads immediately (see the on*Change handlers below).
  useEffect(() => {
    const handle = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(handle);
  }, [search]);

  const loadPage = useCallback(async () => {
    const page = await packagesApi.getPaged(pageIndex, PAGE_SIZE, effectiveBusinessId, debouncedSearch || undefined, typeFilter || undefined);
    // A mutation (e.g. deleting the last item on the last page) can shrink totalPages below the
    // page we're currently viewing — re-fetch the now-last valid page instead of showing empty.
    if (page.items.length === 0 && page.totalPages > 0 && pageIndex > page.totalPages) {
      setPageIndex(page.totalPages);
      return;
    }
    setPaged(page);
  }, [pageIndex, debouncedSearch, effectiveBusinessId, typeFilter]);

  useEffect(() => {
    // Synchronizes with the server whenever paging, search or any filter changes.
    // oxlint-disable-next-line react/set-state-in-effect
    loadPage();
  }, [loadPage]);

  useEffect(() => {
    if (isAdmin) businessesApi.getAll(false).then((list) => setAllBusinesses([...list].sort((a, b) => a.name.localeCompare(b.name))));
  }, [isAdmin]);

  useEffect(() => {
    packageTypesApi.getAll().then((types) => setPackageTypes([...types].sort((a, b) => a.name.localeCompare(b.name))));
  }, []);

  // Admin sees candidates across every business (mirrors the backend's own null-businessId
  // convention); an unassigned manager gets the same never-matches sentinel loadPage uses. Note
  // this never depends on the admin's own businessFilter dropdown — same as the Blazor page.
  //
  // Waits for managedBusinessLoading, same as PackageForm's own loading guard: on first mount (or
  // right after switching business), myBusinessId is briefly null before
  // ManagedBusinessProvider's own fetch resolves — firing with the NEVER_MATCH_ID sentinel in that
  // window hits the server's "only for your own business" check and gets a real 403 for a business
  // id nobody asked about, surfacing as an uncaught rejection since the real id arrives moments
  // later anyway.
  useEffect(() => {
    if (!isAdmin && !isBusinessManager) return;
    if (isBusinessManager && managedBusinessLoading) return;
    const businessId = isBusinessManager ? (myBusinessId ?? NEVER_MATCH_ID) : undefined;
    // oxlint-disable-next-line react/set-state-in-effect
    packagesApi.getMarkdownCandidates(businessId).then((list) => setMarkdownCandidateIds(new Set(list.map((p) => p.id)))).catch(() => {});
    packagesApi.getDonationCandidates(businessId).then((list) => setDonationCandidateIds(new Set(list.map((p) => p.id)))).catch(() => {});
  }, [isAdmin, isBusinessManager, myBusinessId, managedBusinessLoading]);

  function onSearchInput(value: string) {
    setSearch(value);
    setPageIndex(1);
  }

  function onBusinessFilterChange(value: string) {
    setBusinessFilter(value);
    setPageIndex(1);
  }

  function onTypeFilterChange(value: string) {
    setTypeFilter(value);
    setPageIndex(1);
  }

  function clearFilters() {
    setSearch("");
    setDebouncedSearch("");
    setBusinessFilter("");
    setTypeFilter("");
    setPageIndex(1);
  }

  const selectablePackagesOnPage = (paged?.items ?? []).filter((p) => isAdmin || (isBusinessManager && p.businessId === myBusinessId));
  const allSelectableOnPageSelected = selectablePackagesOnPage.length > 0 && selectablePackagesOnPage.every((p) => selectedIds.has(p.id));

  function toggleSelectAll(select: boolean) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      for (const p of selectablePackagesOnPage) {
        if (select) next.add(p.id);
        else next.delete(p.id);
      }
      return next;
    });
  }

  function toggleSelect(id: string, select: boolean) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (select) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  function openAdjustQuantity() {
    setBulkMode("adjustQuantity");
    setBulkQuantityDelta(0);
    setBulkError(null);
  }

  function openExtendPickup() {
    setBulkMode("extendPickup");
    setBulkExtendHours(1);
    setBulkError(null);
  }

  function closeBulkModal() {
    setBulkMode("none");
    setBulkError(null);
  }

  async function duplicateSelected() {
    setBulkBusy(true);
    setBulkError(null);
    try {
      await packagesApi.duplicateMany([...selectedIds]);
      setSelectedIds(new Set());
      await loadPage();
    } catch {
      // e.g. a selected package's business no longer matches the caller's access — close the
      // dialog so the page-level alert is visible, instead of leaving the spinner stuck forever.
      setBulkError("Couldn't duplicate the selected packages. Please refresh and try again.");
    } finally {
      setBulkBusy(false);
      setConfirmingDuplicate(false);
    }
  }

  async function applyBulkModal() {
    if (bulkMode === "adjustQuantity" && bulkQuantityDelta === 0) {
      setBulkError("Enter a non-zero amount.");
      return;
    }
    if (bulkMode === "extendPickup" && bulkExtendHours <= 0) {
      setBulkError("Enter a positive number of hours.");
      return;
    }

    setBulkBusy(true);
    setBulkError(null);
    try {
      if (bulkMode === "adjustQuantity") await packagesApi.adjustQuantityMany([...selectedIds], bulkQuantityDelta);
      else await packagesApi.extendPickupWindowMany([...selectedIds], bulkExtendHours);

      setBulkMode("none");
      setSelectedIds(new Set());
      await loadPage();
    } catch {
      setBulkError("Couldn't apply this to the selected packages. Please refresh and try again.");
    } finally {
      setBulkBusy(false);
    }
  }

  function requestDelete(pkg: PackageDto) {
    if (!isAdmin && pkg.businessId !== myBusinessId) return;
    setPendingDelete(pkg);
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await packagesApi.remove(pendingDelete.id);
      await loadPage();
    } finally {
      setDeleting(false);
      setPendingDelete(null);
    }
  }

  function openHidePrompt(pkg: PackageDto) {
    if (!isAdmin && pkg.businessId !== myBusinessId) return;
    setHidePromptPackage(pkg);
  }

  async function submitHidePrompt(reason: string) {
    if (!hidePromptPackage || !reason.trim()) return;
    setHidePromptBusy(true);
    setHidePromptError(null);
    try {
      await packagesApi.hide(hidePromptPackage.id, reason);
      await loadPage();
      setHidePromptPackage(null);
    } catch {
      // e.g. the caller's business access changed mid-session — surface it instead of leaving the
      // dialog stuck busy with no feedback.
      setHidePromptError("Couldn't hide this package. Please refresh and try again.");
    } finally {
      setHidePromptBusy(false);
    }
  }

  async function unhide(pkg: PackageDto) {
    if (!isAdmin && pkg.businessId !== myBusinessId) return;
    try {
      await packagesApi.unhide(pkg.id);
      await loadPage();
    } catch {
      setBulkError("Couldn't unhide this package. Please refresh and try again.");
    }
  }

  function openDonatePrompt(pkg: PackageDto) {
    if (!isAdmin && pkg.businessId !== myBusinessId) return;
    setDonatePromptPackage(pkg);
  }

  async function confirmDonate() {
    if (!donatePromptPackage) return;
    setDonatePromptBusy(true);
    try {
      await packagesApi.markAsDonated(donatePromptPackage.id);
      setDonationCandidateIds((prev) => {
        const next = new Set(prev);
        next.delete(donatePromptPackage.id);
        return next;
      });
      await loadPage();
    } catch {
      setBulkError("Couldn't mark this package as donated. Please refresh and try again.");
    } finally {
      setDonatePromptBusy(false);
      setDonatePromptPackage(null);
    }
  }

  async function openMarkdownSuggestion(pkg: PackageDto) {
    setMarkdownSuggestionPackage(pkg);
    setMarkdownSuggestion(null);
    setMarkdownSuggestionError(null);
    setMarkdownSuggestionLoading(true);
    try {
      const result = await packagesApi.getMarkdownSuggestion(pkg.id);
      setMarkdownSuggestion(result);
    } catch (err) {
      if (err instanceof ApiError && (err.status === 401 || err.status === 403)) {
        setMarkdownSuggestionError("You don't have access to a price suggestion for this package.");
      } else if (err instanceof ApiError) {
        setMarkdownSuggestionError(err.message || "AI features aren't available right now.");
      } else {
        setMarkdownSuggestionError("Couldn't load a price suggestion. Please try again.");
      }
    } finally {
      setMarkdownSuggestionLoading(false);
    }
  }

  function closeMarkdownSuggestion() {
    setMarkdownSuggestionPackage(null);
    setMarkdownSuggestion(null);
    setMarkdownSuggestionError(null);
  }

  async function dismissMarkdownSuggestion() {
    if (!markdownSuggestionPackage) return;
    setMarkdownDismissBusy(true);
    try {
      await packagesApi.dismissMarkdownSuggestion(markdownSuggestionPackage.id);
      setMarkdownCandidateIds((prev) => {
        const next = new Set(prev);
        next.delete(markdownSuggestionPackage.id);
        return next;
      });
      closeMarkdownSuggestion();
    } catch {
      setMarkdownSuggestionError("Couldn't dismiss this suggestion. Please refresh and try again.");
    } finally {
      setMarkdownDismissBusy(false);
    }
  }

  function pickupLabel(pkg: PackageDto): string {
    return formatPickupWindow(pkg.pickupStart, pkg.pickupEnd, timeZone);
  }

  const canCreate = isAdmin || myBusinessId !== null;
  const hasFilters = search !== "" || businessFilter !== "" || typeFilter !== "";

  return (
    <div>
      <div className="d-flex justify-content-between align-items-start mb-4">
        <div>
          <h1 className="h3 fw-bold mb-1">Packages</h1>
          <p className="text-muted small mb-0">Manage all available food packages</p>
        </div>
        {canCreate && (
          <div className="d-flex gap-2">
            <Link to="/packages/templates" className="btn btn-outline-secondary">
              <i className="bi bi-arrow-repeat me-1" /> Recurring templates
            </Link>
            <Link to="/packages/create" className="btn btn-primary px-4">
              <i className="bi bi-plus-lg me-1" /> Add Package
            </Link>
          </div>
        )}
      </div>

      {paged && (
        <div className="d-flex gap-3 mb-3 flex-wrap">
          <div className="input-group" style={{ maxWidth: 320 }}>
            <span className="input-group-text border-end-0">
              <i className="bi bi-search text-muted" />
            </span>
            <input
              type="text"
              className="form-control border-start-0 ps-0"
              placeholder="Search name or description…"
              value={search}
              onChange={(e) => onSearchInput(e.target.value)}
            />
          </div>
          {isAdmin && (
            <select className="form-select" style={{ maxWidth: 200 }} value={businessFilter} onChange={(e) => onBusinessFilterChange(e.target.value)}>
              <option value="">All businesses</option>
              {allBusinesses.map((b) => (
                <option value={b.id} key={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
          )}
          <select className="form-select" style={{ maxWidth: 180 }} value={typeFilter} onChange={(e) => onTypeFilterChange(e.target.value)}>
            <option value="">All types</option>
            {packageTypes.map((t) => (
              <option value={t.id} key={t.id}>
                {t.name}
              </option>
            ))}
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

      {bulkError && bulkMode === "none" && (
        <div className="alert alert-danger" role="alert">
          {bulkError}
        </div>
      )}

      {selectedIds.size > 0 && (
        <div className="card border-0 shadow-sm bulk-toolbar mb-3">
          <div className="card-body d-flex align-items-center gap-3 flex-wrap py-2">
            <span className="fw-semibold small">{selectedIds.size} selected</span>
            <div className="d-flex gap-2 flex-wrap ms-auto">
              <button className="btn btn-sm btn-outline-secondary" disabled={bulkBusy} onClick={() => setConfirmingDuplicate(true)}>
                <i className="bi bi-copy me-1" /> Duplicate
              </button>
              <button className="btn btn-sm btn-outline-secondary" disabled={bulkBusy} onClick={openAdjustQuantity}>
                <i className="bi bi-dash-slash-lg me-1" /> Adjust quantity
              </button>
              <button className="btn btn-sm btn-outline-secondary" disabled={bulkBusy} onClick={openExtendPickup}>
                <i className="bi bi-clock-history me-1" /> Extend pickup window
              </button>
              <button className="btn btn-sm btn-link text-muted" disabled={bulkBusy} onClick={() => setSelectedIds(new Set())}>
                Clear selection
              </button>
            </div>
          </div>
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
              <i className="bi bi-box-seam" />
            </div>
            <p className="text-muted mb-3">{paged.totalCount === 0 && !hasFilters ? "No packages available yet." : "No packages match your filters."}</p>
            {paged.totalCount === 0 && canCreate && (
              <Link to="/packages/create" className="btn btn-outline-primary">
                Create your first package
              </Link>
            )}
          </div>
        ) : (
          <>
            <div className="table-responsive">
              <table className="table table-hover mb-0 align-middle">
                <thead>
                  <tr className="border-bottom">
                    {(isAdmin || isBusinessManager) && (
                      <th className="ps-4 py-3" style={{ width: "2.5rem" }}>
                        <input
                          type="checkbox"
                          className="form-check-input"
                          checked={allSelectableOnPageSelected}
                          disabled={selectablePackagesOnPage.length === 0}
                          onChange={(e) => toggleSelectAll(e.target.checked)}
                          aria-label="Select all on this page"
                        />
                      </th>
                    )}
                    <th className="px-4 py-3 text-uppercase text-muted small fw-semibold">Name</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Business</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Type</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Price</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Qty</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Pickup</th>
                    {(isAdmin || isBusinessManager) && <th className="py-3 text-uppercase text-muted small fw-semibold">Actions</th>}
                  </tr>
                </thead>
                <tbody>
                  {/* Managers only see packages belonging to their currently selected business here (server-side scoping). */}
                  {paged.items.map((pkg) => {
                    const canManage = isAdmin || (isBusinessManager && pkg.businessId === myBusinessId);
                    return (
                      <tr key={pkg.id}>
                        {(isAdmin || isBusinessManager) && (
                          <td className="ps-4">
                            {canManage && (
                              <input
                                type="checkbox"
                                className="form-check-input"
                                checked={selectedIds.has(pkg.id)}
                                onChange={(e) => toggleSelect(pkg.id, e.target.checked)}
                                aria-label={`Select ${pkg.name}`}
                              />
                            )}
                          </td>
                        )}
                        <td className="px-4" style={{ maxWidth: 380 }}>
                          <div className="fw-semibold text-truncate" title={pkg.name}>
                            {pkg.name}
                            {pkg.templateId !== null && (
                              <span className="badge rounded-pill em-badge-neutral border ms-1" title="Auto-generated daily by a recurring template">
                                <i className="bi bi-arrow-repeat" /> Daily
                              </span>
                            )}
                            {pkg.isHidden && (
                              <span className="badge rounded-pill bg-secondary-subtle text-secondary ms-1" title={pkg.hiddenReason ?? undefined}>
                                <i className="bi bi-eye-slash" /> Hidden
                              </span>
                            )}
                            {pkg.donatedAt !== null ? (
                              <span className="badge rounded-pill bg-success-subtle text-success ms-1" title="Marked as donated — counts toward food-saved impact">
                                <i className="bi bi-heart-fill" /> Donated
                              </span>
                            ) : (
                              canManage &&
                              donationCandidateIds.has(pkg.id) && (
                                <button
                                  type="button"
                                  className="btn btn-sm btn-link p-0 ms-1"
                                  title="Closed with none sold — mark it as donated instead of letting it silently expire"
                                  onClick={() => openDonatePrompt(pkg)}
                                >
                                  <i className="bi bi-heart text-success" />
                                </button>
                              )
                            )}
                          </div>
                          <div className="text-muted small">{pkg.description}</div>
                        </td>
                        <td>{pkg.businessName}</td>
                        <td>
                          <span className="package-type-badge">{pkg.packageTypeName}</span>
                        </td>
                        <td className="fw-semibold">
                          {formatCurrency(pkg.price)}
                          {canManage && markdownCandidateIds.has(pkg.id) && (
                            <button
                              type="button"
                              className="btn btn-sm btn-link p-0 ms-1"
                              title="AI price suggestion available — this package is closing soon with stock left"
                              onClick={() => openMarkdownSuggestion(pkg)}
                            >
                              <i className="bi bi-tag-fill text-warning" />
                            </button>
                          )}
                        </td>
                        <td>{pkg.quantity}</td>
                        <td className="text-muted small">{pickupLabel(pkg)}</td>
                        {(isAdmin || isBusinessManager) && (
                          <td>
                            {canManage && (
                              <div className="d-flex gap-2">
                                <Link to={`/packages/edit/${pkg.id}`} className="btn btn-sm btn-outline-primary" title="Edit">
                                  <i className="bi bi-pencil" />
                                </Link>
                                {pkg.isHidden ? (
                                  <button className="btn btn-sm btn-outline-secondary" title="Unhide" onClick={() => unhide(pkg)}>
                                    <i className="bi bi-eye" />
                                  </button>
                                ) : (
                                  <button className="btn btn-sm btn-outline-secondary" title="Hide from storefront" onClick={() => openHidePrompt(pkg)}>
                                    <i className="bi bi-eye-slash" />
                                  </button>
                                )}
                                <button className="btn btn-sm btn-outline-danger" title="Delete" onClick={() => requestDelete(pkg)}>
                                  <i className="bi bi-trash" />
                                </button>
                              </div>
                            )}
                          </td>
                        )}
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

      <ConfirmDialog
        isOpen={pendingDelete !== null}
        title="Delete package?"
        message={pendingDelete ? `"${pendingDelete.name}" will be permanently removed and can no longer be ordered. This can't be undone.` : ""}
        confirmLabel="Delete package"
        busy={deleting}
        onConfirm={confirmDelete}
        onCancel={() => setPendingDelete(null)}
      />

      <ReportDialog
        isOpen={hidePromptPackage !== null}
        targetLabel={hidePromptPackage?.name ?? ""}
        title={hidePromptPackage ? `Hide "${hidePromptPackage.name}"?` : ""}
        message="It'll disappear from the customer-facing storefront immediately, but stays intact for you to edit or unhide later."
        placeholder="Reason (shown if someone hovers the Hidden badge)"
        confirmLabel="Hide package"
        busy={hidePromptBusy}
        error={hidePromptError}
        onSubmit={submitHidePrompt}
        onCancel={() => {
          setHidePromptPackage(null);
          setHidePromptError(null);
        }}
      />

      <ConfirmDialog
        isOpen={donatePromptPackage !== null}
        title={donatePromptPackage ? `Mark "${donatePromptPackage.name}" as donated?` : ""}
        message="It closed with none sold. Marking it as donated counts its weight toward your food-saved impact instead of letting it just expire — it doesn't change anything else about the package."
        confirmLabel="Mark as donated"
        confirmClass="btn-success"
        busy={donatePromptBusy}
        onConfirm={confirmDonate}
        onCancel={() => setDonatePromptPackage(null)}
      />

      <ConfirmDialog
        isOpen={confirmingDuplicate}
        title="Duplicate packages?"
        message={`${selectedIds.size} package${selectedIds.size === 1 ? "" : "s"} will be copied with the same name, price, and pickup window. Adjust the copies afterward as needed.`}
        confirmLabel="Duplicate"
        confirmClass="btn-primary"
        busy={bulkBusy}
        onConfirm={duplicateSelected}
        onCancel={() => setConfirmingDuplicate(false)}
      />

      {bulkMode !== "none" && (
        <>
          <div className="confirm-backdrop" onClick={closeBulkModal} />
          <div className="confirm-dialog" role="dialog" aria-modal="true" aria-labelledby="bulk-modal-title">
            {bulkMode === "adjustQuantity" ? (
              <>
                <h2 className="confirm-dialog-title" id="bulk-modal-title">
                  Adjust quantity
                </h2>
                <p className="confirm-dialog-message">
                  Applied to {selectedIds.size} selected package{selectedIds.size === 1 ? "" : "s"}. Use a negative number to reduce stock — it won't go below 0.
                </p>
                <div className="mb-3">
                  <label htmlFor="bulkQtyDelta" className="form-label fw-semibold small">
                    Change quantity by
                  </label>
                  <input
                    id="bulkQtyDelta"
                    type="number"
                    className="form-control"
                    value={bulkQuantityDelta}
                    onChange={(e) => setBulkQuantityDelta(Number(e.target.value))}
                  />
                </div>
              </>
            ) : (
              <>
                <h2 className="confirm-dialog-title" id="bulk-modal-title">
                  Extend pickup window
                </h2>
                <p className="confirm-dialog-message">
                  Pushes the pickup end time later for {selectedIds.size} selected package{selectedIds.size === 1 ? "" : "s"}. The start time is unchanged.
                </p>
                <div className="mb-3">
                  <label htmlFor="bulkExtendHours" className="form-label fw-semibold small">
                    Extend by (hours)
                  </label>
                  <input
                    id="bulkExtendHours"
                    type="number"
                    min={0.5}
                    step={0.5}
                    className="form-control"
                    value={bulkExtendHours}
                    onChange={(e) => setBulkExtendHours(Number(e.target.value))}
                  />
                </div>
              </>
            )}
            {bulkError && <p className="text-danger small">{bulkError}</p>}
            <div className="confirm-dialog-actions">
              <button type="button" className="btn btn-outline-secondary" disabled={bulkBusy} onClick={closeBulkModal}>
                Cancel
              </button>
              <button type="button" className="btn btn-primary" disabled={bulkBusy} onClick={applyBulkModal}>
                {bulkBusy && <span className="spinner-border spinner-border-sm me-2" role="status" />}
                Apply
              </button>
            </div>
          </div>
        </>
      )}

      {markdownSuggestionPackage && (
        <>
          <div className="confirm-backdrop" onClick={closeMarkdownSuggestion} />
          <div className="confirm-dialog" role="dialog" aria-modal="true" aria-labelledby="markdown-modal-title">
            <h2 className="confirm-dialog-title" id="markdown-modal-title">
              <i className="bi bi-tag-fill text-warning me-1" /> AI price suggestion — {markdownSuggestionPackage.name}
            </h2>
            {markdownSuggestionLoading ? (
              <div className="d-flex justify-content-center py-4">
                <div className="spinner-border text-primary" role="status">
                  <span className="visually-hidden">Loading...</span>
                </div>
              </div>
            ) : markdownSuggestionError ? (
              <p className="text-danger small mb-0">{markdownSuggestionError}</p>
            ) : markdownSuggestion === null ? (
              <p className="confirm-dialog-message mb-0">No markdown suggested right now — the real sell-through history didn't give the AI a good reason to cut this package's price.</p>
            ) : (
              <>
                <div className="d-flex align-items-baseline gap-2 mb-2">
                  <span className="text-muted text-decoration-line-through">{formatCurrency(markdownSuggestion.currentPrice)}</span>
                  <span className="fw-bold fs-4 text-success">{formatCurrency(markdownSuggestion.suggestedPrice)}</span>
                </div>
                <p className="confirm-dialog-message">{markdownSuggestion.explanation}</p>
                <p className="text-muted small mb-0">Just a suggestion — nothing changes until you edit the package yourself.</p>
              </>
            )}
            <div className="confirm-dialog-actions">
              <button type="button" className="btn btn-outline-secondary" disabled={markdownDismissBusy} onClick={closeMarkdownSuggestion}>
                Close
              </button>
              {markdownSuggestion !== null && (
                <Link to={`/packages/edit/${markdownSuggestionPackage.id}`} className="btn btn-outline-primary">
                  Edit package
                </Link>
              )}
              <button type="button" className="btn btn-primary" disabled={markdownDismissBusy || markdownSuggestionLoading} onClick={dismissMarkdownSuggestion}>
                {markdownDismissBusy && <span className="spinner-border spinner-border-sm me-2" role="status" />}
                Dismiss
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

export default Packages;
