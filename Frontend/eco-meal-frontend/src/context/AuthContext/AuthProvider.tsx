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
  const logoutRef = useRef<() => void>(() => {});

  function logout() {
    localStorage.removeItem(TOKEN_KEY);
    setToken(null);
    setUser(null);
  }

  logoutRef.current = logout;

  useEffect(() => {
    setUnauthorizedHandler(() => {
      logoutRef.current();
      navigate("/account/login", { replace: true });
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
      value={{ user, token, isAuthenticated: !!user, loading, login, logout, refreshUser: loadUser }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export default AuthProvider;
