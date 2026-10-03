import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useSearchParams } from "react-router-dom";
import { describe, expect, it } from "vitest";
import RequireRole from "./RequireRole";
import { AuthContext, type AuthContextValue } from "../context/AuthContext/auth-context";
import type { UserDto } from "../api/models/User";

function LoginStub() {
  const [params] = useSearchParams();
  return <div>Login page / returnUrl={params.get("returnUrl")}</div>;
}

function renderWithAuth(auth: AuthContextValue, initialPath = "/dashboard") {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <AuthContext.Provider value={auth}>
        <Routes>
          <Route path="/account/login" element={<LoginStub />} />
          <Route
            path="/dashboard"
            element={
              <RequireRole roles={["Admin"]}>
                <div>Dashboard content</div>
              </RequireRole>
            }
          />
        </Routes>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

function makeAuth(overrides: Partial<AuthContextValue>): AuthContextValue {
  return {
    user: null,
    token: null,
    isAuthenticated: false,
    loading: false,
    login: () => {},
    logout: () => {},
    refreshUser: async () => {},
    ...overrides,
  };
}

const businessManager: UserDto = { id: "1", name: "Manager", email: "m@example.com", role: "BusinessManager" };
const admin: UserDto = { id: "2", name: "Admin", email: "a@example.com", role: "Admin" };

describe("RequireRole", () => {
  it("redirects anonymous users to login with returnUrl", () => {
    renderWithAuth(makeAuth({}), "/dashboard?tab=stats");

    expect(screen.getByText("Login page / returnUrl=/dashboard?tab=stats")).toBeInTheDocument();
  });

  it("shows Forbidden for a signed-in user with the wrong role", () => {
    renderWithAuth(makeAuth({ user: businessManager, isAuthenticated: true }));

    expect(screen.getByText("Access denied")).toBeInTheDocument();
    expect(screen.queryByText("Dashboard content")).not.toBeInTheDocument();
  });

  it("renders children for a user with the right role", () => {
    renderWithAuth(makeAuth({ user: admin, isAuthenticated: true }));

    expect(screen.getByText("Dashboard content")).toBeInTheDocument();
  });

  it("shows a loading spinner while auth state is still resolving", () => {
    renderWithAuth(makeAuth({ loading: true }));

    expect(screen.getByRole("status")).toBeInTheDocument();
  });
});
