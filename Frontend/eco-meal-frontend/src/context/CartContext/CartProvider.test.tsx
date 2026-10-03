import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it } from "vitest";
import { AuthContext, type AuthContextValue } from "../AuthContext/auth-context";
import type { UserDto } from "../../api/models/User";
import CartProvider from "./CartProvider";
import { useCart } from "./cart-context";

function makeAuth(user: UserDto | null): AuthContextValue {
  return {
    user,
    token: user ? "token" : null,
    isAuthenticated: !!user,
    loading: false,
    login: () => {},
    logout: () => {},
    refreshUser: async () => {},
  };
}

const userA: UserDto = { id: "user-a", name: "A", email: "a@example.com", role: "Customer" };
const userB: UserDto = { id: "user-b", name: "B", email: "b@example.com", role: "Customer" };

const PKG_FROM_A = { id: "pkg-a1", name: "Bread box", price: 10, quantity: 5 };
const PKG_FROM_B = { id: "pkg-b1", name: "Soup box", price: 8, quantity: 5 };

function TestConsumer() {
  const cart = useCart();
  return (
    <div>
      <span data-testid="business">{cart.businessId ?? "none"}</span>
      <span data-testid="count">{cart.totalCount}</span>
      <button onClick={() => cart.addItem("biz-a", "Business A", PKG_FROM_A, 1)}>add-from-a</button>
      <button onClick={() => cart.addItem("biz-b", "Business B", PKG_FROM_B, 1)}>add-from-b</button>
      <button onClick={cart.clear}>clear</button>
    </div>
  );
}

function renderCart(user: UserDto | null) {
  return render(
    <AuthContext.Provider value={makeAuth(user)}>
      <CartProvider>
        <TestConsumer />
      </CartProvider>
    </AuthContext.Provider>,
  );
}

describe("CartProvider", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("keeps only one business's items — adding from a different business replaces the basket", async () => {
    const user = userEvent.setup();
    renderCart(userA);

    await user.click(screen.getByText("add-from-a"));
    expect(screen.getByTestId("business")).toHaveTextContent("biz-a");
    expect(screen.getByTestId("count")).toHaveTextContent("1");

    await user.click(screen.getByText("add-from-b"));
    expect(screen.getByTestId("business")).toHaveTextContent("biz-b");
    expect(screen.getByTestId("count")).toHaveTextContent("1");
  });

  it("persists per-user, so user A's basket never shows for user B", async () => {
    const user = userEvent.setup();
    const { unmount } = renderCart(userA);

    await user.click(screen.getByText("add-from-a"));
    expect(screen.getByTestId("business")).toHaveTextContent("biz-a");
    unmount();

    renderCart(userB);
    expect(screen.getByTestId("business")).toHaveTextContent("none");
    expect(screen.getByTestId("count")).toHaveTextContent("0");

    // User A's own storage key is untouched and still has their basket.
    expect(localStorage.getItem("ecomeal.cart.user-a")).toContain("biz-a");
  });

  it("clears the in-memory basket when the viewer logs out", async () => {
    const user = userEvent.setup();
    const { rerender } = render(
      <AuthContext.Provider value={makeAuth(userA)}>
        <CartProvider>
          <TestConsumer />
        </CartProvider>
      </AuthContext.Provider>,
    );

    await user.click(screen.getByText("add-from-a"));
    expect(screen.getByTestId("business")).toHaveTextContent("biz-a");

    rerender(
      <AuthContext.Provider value={makeAuth(null)}>
        <CartProvider>
          <TestConsumer />
        </CartProvider>
      </AuthContext.Provider>,
    );

    expect(screen.getByTestId("business")).toHaveTextContent("none");
    expect(screen.getByTestId("count")).toHaveTextContent("0");
  });
});
