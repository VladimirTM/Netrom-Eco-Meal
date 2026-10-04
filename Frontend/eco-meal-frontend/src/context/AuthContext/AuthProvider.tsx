import { useEffect, useRef, useState, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import { AuthContext } from "./auth-context";
import type { UserDto } from "../../api/models/User";
import { authApi } from "../../api/clients/AuthApiClient";
import { TOKEN_KEY, setUnauthorizedHandler } from "../../api/base/http";

function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(() => localStorage.getItem(TOKEN_KEY));
  const [user, setUser] = useState<UserDto | null>(null);
  const [loading, setLoading] = useState<boolean>(() => !!localStorage.getItem(TOKEN_KEY));
  const navigate = useNavigate();
  // Ref so the unauthorized handler (registered once below) always calls the latest logout, not a stale closure.
  const logoutRef = useRef<(opts?: { redirectTo?: string }) => void>(() => {});
  // Set by logout({redirectTo}) just before it navigates itself; consumed (read + cleared) by
  // RequireRole so it skips its own competing redirect for that one transition. See the
  // consumeSuppressGuardRedirect doc comment on AuthContextValue for why this exists.
  const suppressGuardRedirectRef = useRef(false);

  function logout(opts?: { redirectTo?: string }) {
    localStorage.removeItem(TOKEN_KEY);
    setToken(null);
    setUser(null);
    if (opts?.redirectTo) {
      suppressGuardRedirectRef.current = true;
      navigate(opts.redirectTo, { replace: true });
    }
  }

  function consumeSuppressGuardRedirect() {
    const value = suppressGuardRedirectRef.current;
    suppressGuardRedirectRef.current = false;
    return value;
  }

  logoutRef.current = logout;

  useEffect(() => {
    setUnauthorizedHandler(() => {
      logoutRef.current({ redirectTo: "/account/login" });
    });
  }, [navigate]);

  async function loadUser() {
    try {
      setUser(await authApi.me());
    } catch {
      localStorage.removeItem(TOKEN_KEY);
      setToken(null);
      setUser(null);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (!token) {
      setUser(null);
      setLoading(false);
      return;
    }
    setLoading(true);
    void loadUser();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token]);

  function login(newToken: string, newUser: UserDto) {
    localStorage.setItem(TOKEN_KEY, newToken);
    setToken(newToken);
    setUser(newUser);
  }

  return (
    <AuthContext.Provider
      value={{ user, token, isAuthenticated: !!user, loading, login, logout, consumeSuppressGuardRedirect, refreshUser: loadUser }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export default AuthProvider;
