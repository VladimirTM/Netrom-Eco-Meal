import { createContext, useContext } from "react";
import type { UserDto } from "../../api/models/User";

export interface AuthContextValue {
  user: UserDto | null;
  token: string | null;
  isAuthenticated: boolean;
  loading: boolean;
  login: (token: string, user: UserDto) => void;
  // redirectTo lets a caller combine clearing auth state with its own navigation (e.g. a custom
  // "password changed" message) atomically. See consumeSuppressGuardRedirect below for why this
  // needs to be atomic rather than two separate calls.
  logout: (opts?: { redirectTo?: string }) => void;
  // RequireRole calls this instead of unconditionally redirecting on its own. logout({redirectTo})
  // sets this before navigating; without it, RequireRole's own <Navigate> (rendered the instant
  // isAuthenticated flips false, while still on the old guarded route) races the caller's intended
  // navigation — and since <Navigate> fires its redirect from a passive effect, which has no
  // guaranteed ordering relative to a timer or even another passive effect, no amount of
  // setTimeout/microtask sequencing on the caller's side can reliably make its own redirect win.
  // Consuming (reading + clearing) the flag here means at most one guarded route's RequireRole
  // ever observes it — exactly the one real race this exists for — and every subsequent
  // anonymous-hits-a-guarded-route case still redirects normally.
  consumeSuppressGuardRedirect: () => boolean;
  refreshUser: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside AuthProvider");
  return ctx;
}
