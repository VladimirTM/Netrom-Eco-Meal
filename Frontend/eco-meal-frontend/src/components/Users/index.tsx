import { useCallback, useEffect, useState } from "react";
import { ApiError } from "../../api/base/http";
import { businessesApi, type StaffMemberDto } from "../../api/clients/BusinessesApiClient";
import { usersApi, type UserWithRoleDto } from "../../api/clients/UsersApiClient";
import type { BusinessDto } from "../../api/models/Business";
import type { PaginatedList } from "../../api/models/Pagination";
import { useAuth } from "../../context/AuthContext/auth-context";
import AnchoredDropdown from "../common/AnchoredDropdown";
import Pagination from "../common/Pagination";

const PAGE_SIZE = 10;
const ALL_ROLES = ["Admin", "Customer", "BusinessManager"];

function initial(name: string): string {
  return name.trim().charAt(0).toUpperCase() || "?";
}

function displayRole(role: string): string {
  return role === "BusinessManager" ? "Business Manager" : role;
}

function roleBadgeClass(role: string): string {
  switch (role) {
    case "Admin":
      return "role-badge-admin";
    case "BusinessManager":
      return "role-badge-businessmanager";
    default:
      return "role-badge-customer";
  }
}

function roleDotColor(role: string): string {
  switch (role) {
    case "Admin":
      return "#7c3aed";
    case "BusinessManager":
      return "#2563eb";
    default:
      return "#16a34a";
  }
}

// Ports Users.razor (/users): search/role filter over a paginated user list, a role-change
// dropdown (disabled for your own row — you can't change your own role), and, for
// BusinessManager rows, a business-assignment dropdown.
function Users() {
  const { user: currentUser } = useAuth();

  const [paged, setPaged] = useState<PaginatedList<UserWithRoleDto> | null>(null);
  const [businesses, setBusinesses] = useState<BusinessDto[]>([]);
  const [staffByBusiness, setStaffByBusiness] = useState<Record<string, StaffMemberDto[]>>({});
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [roleFilter, setRoleFilter] = useState("");
  const [pageIndex, setPageIndex] = useState(1);
  const [openDropdownUserId, setOpenDropdownUserId] = useState<string | null>(null);
  const [openBusinessDropdownUserId, setOpenBusinessDropdownUserId] = useState<string | null>(null);
  const [roleError, setRoleError] = useState<string | null>(null);

  const loadLookups = useCallback(async () => {
    const all = await businessesApi.getAll();
    setBusinesses(all);
    const staffEntries = await Promise.all(all.map(async (b) => [b.id, await businessesApi.getStaff(b.id)] as const));
    setStaffByBusiness(Object.fromEntries(staffEntries));
  }, []);

  useEffect(() => {
    // oxlint-disable-next-line react/set-state-in-effect
    loadLookups();
  }, [loadLookups]);

  // Mirrors the Blazor page's own Debouncer — search reloads 300ms after the last keystroke, the
  // role filter reloads immediately (see onRoleFilterChange).
  useEffect(() => {
    const handle = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(handle);
  }, [search]);

  const loadPage = useCallback(async () => {
    const page = await usersApi.getPaged(pageIndex, PAGE_SIZE, debouncedSearch, roleFilter || undefined);
    setPaged(page);
  }, [pageIndex, debouncedSearch, roleFilter]);

  useEffect(() => {
    // oxlint-disable-next-line react/set-state-in-effect
    loadPage();
  }, [loadPage]);

  function onSearchChange(value: string) {
    setSearch(value);
    setPageIndex(1);
  }

  function onRoleFilterChange(value: string) {
    setRoleFilter(value);
    setPageIndex(1);
  }

  function clearFilters() {
    setSearch("");
    setRoleFilter("");
    setPageIndex(1);
  }

  const hasFilters = search !== "" || roleFilter !== "";

  function assignedBusinesses(userId: string): BusinessDto[] {
    return businesses.filter((b) => (staffByBusiness[b.id] ?? []).some((s) => s.id === userId));
  }

  // Role and business dropdowns are mutually exclusive — opening one closes the other.
  function toggleDropdown(userId: string) {
    setOpenBusinessDropdownUserId(null);
    setOpenDropdownUserId((cur) => (cur === userId ? null : userId));
  }

  function toggleBusinessDropdown(userId: string) {
    setOpenDropdownUserId(null);
    setOpenBusinessDropdownUserId((cur) => (cur === userId ? null : userId));
  }

  async function refreshStaff(businessId: string) {
    const staff = await businessesApi.getStaff(businessId);
    setStaffByBusiness((prev) => ({ ...prev, [businessId]: staff }));
  }

  async function setRole(target: UserWithRoleDto, role: string) {
    setOpenDropdownUserId(null);
    setRoleError(null);
    if (role === target.role) return;

    try {
      await usersApi.updateRole(target.id, role);
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        setRoleError(err.message);
        return;
      }
      throw err;
    }

    // Reload rather than patch the row in place — the user's new role may no longer match the
    // active roleFilter, and patching would leave it visible until the next reload.
    await loadPage();
    // Moving away from BusinessManager may have auto-released a business server-side.
    await loadLookups();
  }

  // Deliberately doesn't close the dropdown — an admin can add several businesses in one open.
  async function addBusiness(target: UserWithRoleDto, businessId: string) {
    setRoleError(null);
    try {
      await businessesApi.addStaff(businessId, target.id, target.name);
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        setRoleError("That staff assignment already exists. Please try again.");
      } else {
        throw err;
      }
    }
    await refreshStaff(businessId);
  }

  async function removeBusiness(target: UserWithRoleDto, businessId: string) {
    setRoleError(null);
    await businessesApi.removeStaff(businessId, target.id);
    await refreshStaff(businessId);
  }

  return (
    <div>
      <div className="d-flex justify-content-between align-items-start mb-4">
        <div>
          <h1 className="h3 fw-bold mb-1">User Roles</h1>
          <p className="text-muted small mb-0">Control what each person can do across Eco Meal</p>
        </div>
      </div>

      {roleError && (
        <div className="alert alert-danger" role="alert">
          {roleError}
        </div>
      )}

      {paged && (
        <div className="d-flex gap-3 mb-3 flex-wrap">
          <div className="input-group" style={{ maxWidth: 320 }}>
            <span className="input-group-text border-end-0">
              <i className="bi bi-search text-muted" />
            </span>
            <input type="text" className="form-control border-start-0 ps-0" placeholder="Search name, email…" value={search} onChange={(e) => onSearchChange(e.target.value)} />
          </div>
          <select className="form-select" style={{ maxWidth: 200 }} value={roleFilter} onChange={(e) => onRoleFilterChange(e.target.value)}>
            <option value="">All roles</option>
            {ALL_ROLES.map((role) => (
              <option value={role} key={role}>
                {displayRole(role)}
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
              <i className="bi bi-people" />
            </div>
            <p className="text-muted mb-0">{paged.totalCount === 0 && !hasFilters ? "No users registered yet." : "No users match your filters."}</p>
          </div>
        ) : (
          <>
            <div className="table-responsive">
              <table className="table table-hover mb-0 align-middle">
                <thead>
                  <tr className="border-bottom">
                    <th className="px-4 py-3 text-uppercase text-muted small fw-semibold">User</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Role</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Businesses</th>
                  </tr>
                </thead>
                <tbody>
                  {paged.items.map((u) => {
                    const isSelf = u.id === currentUser?.id;
                    return (
                      <tr key={u.id}>
                        <td className="px-4">
                          <div className="d-flex align-items-center gap-3">
                            <div className="sidebar-user-avatar" style={{ color: "#16a34a", background: "rgba(22,163,74,0.12)" }}>
                              {initial(u.name)}
                            </div>
                            <div>
                              <div className="fw-semibold d-flex align-items-center gap-2">
                                {u.name}
                                {isSelf && <span className="badge rounded-pill em-badge-neutral border small fw-normal">You</span>}
                              </div>
                              <div className="text-muted small">{u.email}</div>
                            </div>
                          </div>
                        </td>
                        <td className="position-relative">
                          <AnchoredDropdown
                            triggerClass={`role-badge ${roleBadgeClass(u.role)}`}
                            disabled={isSelf}
                            title={isSelf ? "You can't change your own role" : "Change role"}
                            isOpen={openDropdownUserId === u.id}
                            onToggle={() => toggleDropdown(u.id)}
                            onClose={() => setOpenDropdownUserId(null)}
                            trigger={
                              <>
                                <span className="role-option-dot" style={{ backgroundColor: roleDotColor(u.role) }} />
                                {displayRole(u.role)}
                                {!isSelf && <i className="bi bi-chevron-down" style={{ fontSize: "0.65rem" }} />}
                              </>
                            }
                          >
                            {ALL_ROLES.map((role) => (
                              <button type="button" className="role-option" key={role} onClick={() => setRole(u, role)}>
                                <span className="role-option-dot" style={{ backgroundColor: roleDotColor(role) }} />
                                {displayRole(role)}
                                {role === u.role && <i className="bi bi-check-lg ms-auto text-muted" />}
                              </button>
                            ))}
                          </AnchoredDropdown>
                        </td>
                        <td className="position-relative">
                          {u.role !== "BusinessManager" ? (
                            <span className="text-muted small">—</span>
                          ) : (
                            (() => {
                              const assigned = assignedBusinesses(u.id);
                              const assignedIds = new Set(assigned.map((b) => b.id));
                              const available = businesses.filter((b) => !assignedIds.has(b.id));
                              return (
                                <div className="d-flex flex-wrap align-items-center gap-1">
                                  {assigned.map((business) => (
                                    <span className="role-badge role-badge-businessmanager" key={business.id}>
                                      {business.name}
                                      <button type="button" className="btn-close" style={{ fontSize: "0.55rem" }} title="Remove" onClick={() => removeBusiness(u, business.id)} />
                                    </span>
                                  ))}
                                  <AnchoredDropdown
                                    triggerClass="role-badge role-badge-unassigned"
                                    title="Add business"
                                    isOpen={openBusinessDropdownUserId === u.id}
                                    onToggle={() => toggleBusinessDropdown(u.id)}
                                    onClose={() => setOpenBusinessDropdownUserId(null)}
                                    trigger={<i className="bi bi-plus-lg" />}
                                  >
                                    {available.length === 0 ? (
                                      <div className="role-option text-muted">No more businesses to add</div>
                                    ) : (
                                      available.map((business) => (
                                        <button type="button" className="role-option" key={business.id} onClick={() => addBusiness(u, business.id)}>
                                          <span className="role-option-dot" style={{ backgroundColor: "#2563eb" }} />
                                          {business.name}
                                        </button>
                                      ))
                                    )}
                                  </AnchoredDropdown>
                                </div>
                              );
                            })()
                          )}
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
    </div>
  );
}

export default Users;
