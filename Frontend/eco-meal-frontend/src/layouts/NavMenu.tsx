import { useState } from "react";
import { NavLink } from "react-router-dom";
import { useAuth } from "../context/AuthContext/auth-context";
import { useManagedBusiness } from "../context/ManagedBusinessContext/managed-business-context";
import ThemeToggle from "../components/common/ThemeToggle";
import NotificationBell from "../components/common/NotificationBell";
import AnchoredDropdown from "../components/common/AnchoredDropdown";

function initial(name: string | undefined): string {
  return (name ?? "").trim().charAt(0).toUpperCase() || "?";
}

function displayRole(role: string | undefined): string {
  switch (role) {
    case "Admin":
      return "Admin";
    case "BusinessManager":
      return "Business Manager";
    case "Customer":
      return "Customer";
    default:
      return "Signed in";
  }
}

function navLinkClass({ isActive }: { isActive: boolean }): string {
  return `nav-link${isActive ? " active" : ""}`;
}

// Mirrors Blazor's NavMenu.razor — the sidebar shared by Admin and BusinessManager.
function NavMenu() {
  const { user, logout } = useAuth();
  const { myBusinesses, selectedBusinessId, select } = useManagedBusiness();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [switcherOpen, setSwitcherOpen] = useState(false);
  const isAdmin = user?.role === "Admin";

  const selectedBusinessName = myBusinesses.find((b) => b.id === selectedBusinessId)?.name ?? "Select a business";

  function closeMobileNav() {
    setMobileOpen(false);
  }

  return (
    <>
      <div className="sidebar-brand py-4">
        <NavLink to="/dashboard" className="text-decoration-none d-flex align-items-center justify-content-center gap-2">
          <img src="/logo.png" alt="Eco Meal" className="brand-logo" />
          <div>
            <span className="fw-bold text-white d-block lh-1" style={{ fontSize: "0.95rem" }}>
              Eco Meal
            </span>
            <span className="text-white-50" style={{ fontSize: "0.72rem" }}>
              {isAdmin ? "Admin Panel" : "Manager Panel"}
            </span>
          </div>
        </NavLink>
      </div>

      <button
        type="button"
        className="navbar-toggler"
        title="Navigation menu"
        aria-label="Navigation menu"
        aria-expanded={mobileOpen}
        onClick={() => setMobileOpen((v) => !v)}
      >
        <i className="bi bi-list" />
      </button>

      <div className={`nav-scrollable${mobileOpen ? " nav-scrollable-open" : ""}`} onClick={closeMobileNav}>
        <nav className="nav flex-column px-2 pb-3">
          {myBusinesses.length > 1 && (
            <div className="nav-item position-relative px-2 pb-2">
              <AnchoredDropdown
                triggerClass="role-badge role-badge-businessmanager w-100 d-flex justify-content-between align-items-center"
                title="Switch business"
                isOpen={switcherOpen}
                onToggle={() => setSwitcherOpen((v) => !v)}
                onClose={() => setSwitcherOpen(false)}
                trigger={
                  <>
                    <span className="text-truncate">{selectedBusinessName}</span>
                    <i className="bi bi-chevron-down" style={{ fontSize: "0.65rem" }} />
                  </>
                }
              >
                {myBusinesses.map((business) => (
                  <button
                    type="button"
                    className="role-option"
                    key={business.id}
                    onClick={() => {
                      setSwitcherOpen(false);
                      select(business.id);
                    }}
                  >
                    <span className="role-option-dot" style={{ backgroundColor: "#2563eb" }} />
                    {business.name}
                    {business.id === selectedBusinessId && <i className="bi bi-check-lg ms-auto text-muted" />}
                  </button>
                ))}
              </AnchoredDropdown>
            </div>
          )}
          {myBusinesses.length === 1 && (
            <div className="nav-item px-2 pb-2 text-white-50 small text-truncate">
              <i className="bi bi-building me-2" />
              {selectedBusinessName}
            </div>
          )}

          <div className="nav-section-label">Main</div>

          <div className="nav-item">
            <NavLink className={navLinkClass} to="/dashboard">
              <i className="bi bi-house-door me-2" />Dashboard
            </NavLink>
          </div>

          <div className="nav-item">
            <NavLink className={navLinkClass} to="/businesses">
              <i className="bi bi-building me-2" />Businesses
            </NavLink>
          </div>

          <div className="nav-item">
            <NavLink className={navLinkClass} to="/packages">
              <i className="bi bi-box-seam me-2" />Packages
            </NavLink>
          </div>

          <div className="nav-item">
            <NavLink className={navLinkClass} to="/orders/manage">
              <i className="bi bi-cart3 me-2" />Orders
            </NavLink>
          </div>

          <div className="nav-item">
            <NavLink className={navLinkClass} to="/payments">
              <i className="bi bi-credit-card me-2" />Payments
            </NavLink>
          </div>

          <div className="nav-item">
            <NavLink className={navLinkClass} to="/account/settings">
              <i className="bi bi-person-gear me-2" />Account Settings
            </NavLink>
          </div>

          {isAdmin && (
            <>
              <div className="nav-section-label">Admin</div>

              <div className="nav-item">
                <NavLink className={navLinkClass} to="/users">
                  <i className="bi bi-shield-lock me-2" />User Roles
                </NavLink>
              </div>

              <div className="nav-item">
                <NavLink className={navLinkClass} to="/reports">
                  <i className="bi bi-flag me-2" />Reports
                </NavLink>
              </div>

              <div className="nav-item">
                <NavLink className={navLinkClass} to="/audit-log">
                  <i className="bi bi-journal-text me-2" />Audit Log
                </NavLink>
              </div>

              <div className="nav-item">
                <NavLink className={navLinkClass} to="/types">
                  <i className="bi bi-tags me-2" />Types
                </NavLink>
              </div>
            </>
          )}
        </nav>
      </div>

      <div className="sidebar-footer">
        <div className="sidebar-user-avatar">{initial(user?.name)}</div>
        <div className="sidebar-user-info">
          <div className="sidebar-user-email" title={user?.email}>{user?.email}</div>
          <div className="sidebar-user-role">{displayRole(user?.role)}</div>
        </div>
        <NotificationBell triggerClass="sidebar-notif-btn" />
        <ThemeToggle triggerClass="sidebar-notif-btn" />
        <button type="button" className="sidebar-logout-btn" title="Sign out" aria-label="Sign out" onClick={logout}>
          <i className="bi bi-box-arrow-right" />
        </button>
      </div>
    </>
  );
}

export default NavMenu;
