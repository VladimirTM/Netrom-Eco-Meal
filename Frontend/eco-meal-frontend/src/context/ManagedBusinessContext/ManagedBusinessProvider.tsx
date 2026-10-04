import { useCallback, useEffect, useState, type ReactNode } from "react";
import { businessesApi } from "../../api/clients/BusinessesApiClient";
import type { BusinessDto } from "../../api/models/Business";
import { useAuth } from "../AuthContext/auth-context";
import { ManagedBusinessContext } from "./managed-business-context";

function storageKey(userId: string): string {
  return `ecomeal.managedBusiness.${userId}`;
}

function loadStoredSelection(userId: string): string | null {
  try {
    return localStorage.getItem(storageKey(userId));
  } catch {
    return null;
  }
}

// Mirrors Backend/NetromEcoMeal.Web/Services/ManagedBusinessContext.cs — "which of my businesses
// am I managing right now," for a BusinessManager staffed at more than one. Admins have no
// "my business" concept (they manage every business through /businesses directly), so this never
// fetches for them — Dashboard/Packages/Orders/Payments all treat a null selectedBusinessId as
// "no business filter" for an Admin, same as the Blazor pages' own _isAdmin branches.
function ManagedBusinessProvider({ children }: { children: ReactNode }) {
  const { user, loading: authLoading } = useAuth();
  const [myBusinesses, setMyBusinesses] = useState<BusinessDto[]>([]);
  const [selectedBusinessId, setSelectedBusinessId] = useState<string | null>(null);
  const [loadedForUserId, setLoadedForUserId] = useState<string | null>(null);

  const userId = user?.id ?? null;
  const isManager = user?.role === "BusinessManager";

  // `loading` is derived, not a separate imperative flag — that's what closes the race a plain
  // useState(false) had: on first mount (a hard navigation/reload), this provider's own fetch
  // effect hasn't run yet, but descendant effects (e.g. PackageForm's own "do I manage a
  // business?" check) fire first — React commits effects bottom-up. A consumer reading a
  // not-yet-started fetch as "not loading" would wrongly conclude "zero businesses" and could get
  // stuck there (nothing later resets a one-shot forbidden flag). Deriving `loading` straight from
  // "do we know the user yet" and "have we fetched for this exact user yet" can't go stale that way.
  const loading = authLoading || (isManager && userId !== loadedForUserId);

  // A non-manager (or a logged-out viewer) has nothing to fetch — settle immediately, without a
  // useEffect round trip, same "adjusting state when a prop changes" pattern CartProvider uses for
  // its own per-user reload.
  if (userId !== loadedForUserId && !isManager) {
    setLoadedForUserId(userId);
    setMyBusinesses([]);
    setSelectedBusinessId(null);
  }

  useEffect(() => {
    if (!userId || !isManager) return;

    let cancelled = false;
    businessesApi.getPaged({ staffUserId: userId, pageSize: 100 }).then((page) => {
      if (cancelled) return;
      const mine = [...page.items].sort((a, b) => a.name.localeCompare(b.name));
      setMyBusinesses(mine);
      const stored = loadStoredSelection(userId);
      const next = mine.some((b) => b.id === stored) ? stored : (mine[0]?.id ?? null);
      setSelectedBusinessId(next);
      setLoadedForUserId(userId);
    });
    return () => {
      cancelled = true;
    };
  }, [userId, isManager]);

  const select = useCallback(
    (businessId: string) => {
      setSelectedBusinessId((prev) => {
        if (prev === businessId) return prev;
        if (userId) {
          try {
            localStorage.setItem(storageKey(userId), businessId);
          } catch {
            // Best-effort: the in-memory selection stays authoritative even if this write fails.
          }
        }
        return businessId;
      });
    },
    [userId],
  );

  return (
    <ManagedBusinessContext.Provider value={{ myBusinesses, selectedBusinessId, loading, select }}>
      {children}
    </ManagedBusinessContext.Provider>
  );
}

export default ManagedBusinessProvider;
