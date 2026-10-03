import { Link, Outlet } from "react-router-dom";
import { useAuth } from "../context/AuthContext/auth-context";
import ThemeToggle from "../components/common/ThemeToggle";

const currentYear = new Date().getFullYear();

// Mirrors Blazor's PublicLayout.razor — the customer-facing header/footer shell. Feature icons
// (cart, notifications, orders, ...) are added as their own phases bring in the pages behind
// them; Phase 5 adds impact/brands/plan-basket, the three it owns.
function PublicLayout() {
  const { user, isAuthenticated, logout } = useAuth();
  const isStaff = user?.role === "Admin" || user?.role === "BusinessManager";
  const isCustomer = user?.role === "Customer";

  return (
    <div className="public-shell">
      <header className="public-header">
        <div className="public-header-inner">
          <Link to="/" className="public-brand text-decoration-none">
            <img src="/logo.png" alt="" className="public-brand-logo" />
            <span className="public-brand-name">Eco Meal</span>
          </Link>

          {isAuthenticated ? (
            <div className="d-flex align-items-center gap-2">
              <Link to="/impact" className="public-cart-btn" title="Community impact leaderboard" aria-label="Community impact leaderboard">
                <i className="bi bi-trophy" />
              </Link>
              <Link to="/brands" className="public-cart-btn" title="Browse brands" aria-label="Browse brands">
                <i className="bi bi-diagram-3" />
              </Link>
              {isCustomer && (
                <Link to="/plan-basket" className="public-cart-btn" title="Plan a basket with AI" aria-label="Plan a basket with AI">
                  <i className="bi bi-stars" />
                </Link>
              )}
              {isStaff && (
                <Link to="/dashboard" className="public-cart-btn" title="Dashboard" aria-label="Dashboard">
                  <i className="bi bi-house-door" />
                </Link>
              )}
              <Link to="/account/settings" className="public-cart-btn" title="Account settings" aria-label="Account settings">
                <i className="bi bi-person-gear" />
              </Link>
              <ThemeToggle triggerClass="public-cart-btn" />
              <button type="button" className="public-header-logout" title="Sign out" onClick={logout}>
                <i className="bi bi-box-arrow-right" />
              </button>
            </div>
          ) : (
            <div className="d-flex align-items-center gap-2">
              <Link to="/impact" className="public-cart-btn" title="Community impact leaderboard" aria-label="Community impact leaderboard">
                <i className="bi bi-trophy" />
              </Link>
              <Link to="/brands" className="public-cart-btn" title="Browse brands" aria-label="Browse brands">
                <i className="bi bi-diagram-3" />
              </Link>
              <ThemeToggle triggerClass="public-cart-btn" />
              <Link to="/account/login" className="public-header-btn public-header-btn-ghost">
                Sign in
              </Link>
              <Link to="/account/register" className="public-header-btn public-header-btn-primary">
                Create account
              </Link>
            </div>
          )}
        </div>
      </header>

      <main className="public-main">
        <Outlet />
      </main>

      <footer className="public-footer">
        <div className="public-footer-inner">
          <div className="d-flex align-items-center justify-content-center gap-2">
            <img src="/logo.png" alt="" className="public-footer-logo" />
            <span className="public-footer-brand">Eco Meal</span>
          </div>
          <p className="public-footer-tagline">Surplus food from local kitchens, rescued before it's binned.</p>
          <p className="public-footer-copy">&copy; {currentYear} Eco Meal</p>
        </div>
      </footer>
    </div>
  );
}

export default PublicLayout;
