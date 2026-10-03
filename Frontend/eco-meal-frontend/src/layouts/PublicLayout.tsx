import { Link, Outlet } from "react-router-dom";
import { useAuth } from "../context/AuthContext/auth-context";
import { useCart } from "../context/CartContext/cart-context";
import CartPanel from "../components/common/CartPanel";
import NotificationBell from "../components/common/NotificationBell";
import NotificationPanel from "../components/common/NotificationPanel";
import ThemeToggle from "../components/common/ThemeToggle";

const currentYear = new Date().getFullYear();

// Mirrors Blazor's PublicLayout.razor — the customer-facing header/footer shell.
function PublicLayout() {
  const { user, isAuthenticated, logout } = useAuth();
  const cart = useCart();
  const isStaff = user?.role === "Admin" || user?.role === "BusinessManager";
  const isCustomer = user?.role === "Customer";
  const canApplyAsBusiness = isCustomer || user?.role === "BusinessManager";

  return (
    <>
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
                <NotificationBell triggerClass="public-cart-btn" />
                {isCustomer && (
                  <>
                    <Link to="/plan-basket" className="public-cart-btn" title="Plan a basket with AI" aria-label="Plan a basket with AI">
                      <i className="bi bi-stars" />
                    </Link>
                    <Link to="/orders" className="public-cart-btn" title="Your orders" aria-label="Your orders">
                      <i className="bi bi-receipt" />
                    </Link>
                    <Link to="/trip-planner" className="public-cart-btn" title="Plan your pickup route" aria-label="Plan your pickup route">
                      <i className="bi bi-signpost-2" />
                    </Link>
                    <Link to="/circles" className="public-cart-btn" title="Your Rescue Circles" aria-label="Your Rescue Circles">
                      <i className="bi bi-people" />
                    </Link>
                    <Link to="/standing-orders" className="public-cart-btn" title="Your standing orders" aria-label="Your standing orders">
                      <i className="bi bi-arrow-repeat" />
                    </Link>
                    <Link to="/referrals" className="public-cart-btn" title="Invite a friend" aria-label="Invite a friend">
                      <i className="bi bi-gift" />
                    </Link>
                    <button type="button" className="public-cart-btn" onClick={cart.open} aria-label="Open your basket">
                      <i className="bi bi-basket2" />
                      {cart.totalCount > 0 && <span className="public-cart-badge">{cart.totalCount}</span>}
                    </button>
                  </>
                )}
                {isStaff && (
                  <Link to="/dashboard" className="public-cart-btn" title="Dashboard" aria-label="Dashboard">
                    <i className="bi bi-house-door" />
                  </Link>
                )}
                {canApplyAsBusiness && (
                  <Link to="/businesses/apply" className="public-cart-btn" title="List your business" aria-label="List your business">
                    <i className="bi bi-shop-window" />
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

      {/* Outside .public-shell on purpose — a position:fixed panel nested under the header's
          position:sticky stacking context gets trapped behind <main> regardless of z-index. */}
      {isCustomer && <CartPanel />}
      {isAuthenticated && <NotificationPanel />}
    </>
  );
}

export default PublicLayout;
