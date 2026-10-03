import { useAuth } from "../../context/AuthContext/auth-context";

// Placeholder for Phase 5 — this phase only needs the chrome (header/footer/theme/auth) to prove
// the app shell works end to end; the real browsing experience lands with the customer pages.
function Home() {
  const { user } = useAuth();

  return (
    <div className="text-center py-5">
      <h1 className="h2 fw-bold mb-2">Eco Meal</h1>
      <p className="text-muted">
        {user ? `Welcome back, ${user.name}.` : "Surplus food from local kitchens, rescued before it's binned."}
      </p>
    </div>
  );
}

export default Home;
