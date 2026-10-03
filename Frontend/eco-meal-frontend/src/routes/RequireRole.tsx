import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../context/AuthContext/auth-context";
import type { AppRole } from "../api/models/User";
import LoadingSpinner from "../components/common/LoadingSpinner";
import ForbiddenPanel from "../components/common/ForbiddenPanel";

interface RequireRoleProps {
  roles?: AppRole[];
  children: ReactNode;
}

// Generalizes Blazor's ProtectedRoute/AdminRoute, and Routes.razor's own NotAuthorized branch:
// signed in but wrong role shows ForbiddenPanel inline, not signed in at all redirects to login.
function RequireRole({ roles, children }: RequireRoleProps) {
  const { isAuthenticated, loading, user } = useAuth();
  const location = useLocation();

  if (loading) return <LoadingSpinner />;

  if (!isAuthenticated) {
    const returnUrl = encodeURIComponent(location.pathname + location.search);
    return <Navigate to={`/account/login?returnUrl=${returnUrl}`} replace />;
  }

  if (roles && roles.length > 0 && !roles.includes(user!.role)) {
    return <ForbiddenPanel message="Your account doesn't have permission to view this page." backHref="/" backLabel="Back to home" />;
  }

  return <>{children}</>;
}

export default RequireRole;
