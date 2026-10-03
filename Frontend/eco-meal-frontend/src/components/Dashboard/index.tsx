import { useAuth } from "../../context/AuthContext/auth-context";

// Placeholder for Phase 7 — same reasoning as Home: this phase only proves the dashboard shell
// (sidebar, role-aware nav, auth) renders correctly per role.
function Dashboard() {
  const { user } = useAuth();

  return (
    <div>
      <h1 className="h3 fw-bold mb-1">Dashboard</h1>
      <p className="text-muted">Welcome back, {user?.name}.</p>
    </div>
  );
}

export default Dashboard;
