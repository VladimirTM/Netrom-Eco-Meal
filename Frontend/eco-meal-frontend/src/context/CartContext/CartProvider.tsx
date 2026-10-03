import { useCallback, useEffect, useState, type ReactNode } from "react";
import { useAuth } from "../AuthContext/auth-context";
import { CartContext, type AddToCartPackage, type CartLineItem } from "./cart-context";

interface StoredCart {
  businessId: string | null;
  businessName: string | null;
  items: CartLineItem[];
  logisticsNote: string;
}

const EMPTY_CART: StoredCart = { businessId: null, businessName: null, items: [], logisticsNote: "" };

function storageKey(userId: string): string {
  return `ecomeal.cart.${userId}`;
}

function loadCart(userId: string): StoredCart {
  try {
    const raw = localStorage.getItem(storageKey(userId));
    if (!raw) return EMPTY_CART;
    const parsed = JSON.parse(raw) as StoredCart;
    return { ...EMPTY_CART, ...parsed };
  } catch {
    return EMPTY_CART;
  }
}

// Mirrors CartService (Backend/NetromEcoMeal.Web/Services/CartService.cs) — single business per
// basket, persisted per signed-in user (ecomeal.cart.{userId}) so switching accounts in the same
// browser never leaks one customer's basket into another's. Cleared entirely on logout.
function CartProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const [cart, setCart] = useState<StoredCart>(() => (user ? loadCart(user.id) : EMPTY_CART));
  const [isOpen, setIsOpen] = useState(false);

  // Reloads (or clears, on logout) the basket the instant the signed-in user changes, without a
  // useEffect round trip — same "adjusting state when a prop changes" pattern PackageDetailModal
  // uses. Keyed on user id, not the whole user object, so re-fetching /auth/me doesn't reset it.
  const [loadedForUserId, setLoadedForUserId] = useState<string | null>(user?.id ?? null);
  if ((user?.id ?? null) !== loadedForUserId) {
    setLoadedForUserId(user?.id ?? null);
    setCart(user ? loadCart(user.id) : EMPTY_CART);
    setIsOpen(false);
  }

  useEffect(() => {
    if (!user) return;
    try {
      localStorage.setItem(storageKey(user.id), JSON.stringify(cart));
    } catch {
      // Best-effort persistence — a full/blocked localStorage shouldn't break the basket in-memory.
    }
  }, [user, cart]);

  const wouldReplaceCart = useCallback((businessId: string) => cart.businessId !== null && cart.businessId !== businessId, [cart.businessId]);

  const inBasketQuantity = useCallback(
    (packageId: string) => cart.items.find((i) => i.packageId === packageId)?.quantity ?? 0,
    [cart.items],
  );

  // Replaces the cart outright if adding from a different business — callers are expected to
  // have already confirmed that with the viewer via wouldReplaceCart + ConfirmDialog.
  function addItem(businessId: string, businessName: string, pkg: AddToCartPackage, quantity: number) {
    setCart((prev) => {
      const sameBusinessItems = prev.businessId === businessId ? prev.items : [];
      const existing = sameBusinessItems.find((i) => i.packageId === pkg.id);
      const nextQuantity = Math.max(0, Math.min(pkg.quantity, (existing?.quantity ?? 0) + quantity));

      const items = existing
        ? sameBusinessItems.map((i) => (i.packageId === pkg.id ? { ...i, quantity: nextQuantity, maxQuantity: pkg.quantity } : i))
        : [...sameBusinessItems, { packageId: pkg.id, name: pkg.name, unitPrice: pkg.price, quantity: nextQuantity, maxQuantity: pkg.quantity }];

      return {
        businessId,
        businessName,
        items,
        logisticsNote: prev.businessId === businessId ? prev.logisticsNote : "",
      };
    });
  }

  function setQuantity(packageId: string, quantity: number) {
    setCart((prev) => {
      if (quantity <= 0) return { ...prev, items: prev.items.filter((i) => i.packageId !== packageId) };
      return {
        ...prev,
        items: prev.items.map((i) => (i.packageId === packageId ? { ...i, quantity: Math.min(quantity, i.maxQuantity) } : i)),
      };
    });
  }

  function removeItem(packageId: string) {
    setCart((prev) => ({ ...prev, items: prev.items.filter((i) => i.packageId !== packageId) }));
  }

  function clear() {
    setCart(EMPTY_CART);
  }

  function setLogisticsNote(note: string) {
    setCart((prev) => ({ ...prev, logisticsNote: note }));
  }

  const totalCount = cart.items.reduce((sum, i) => sum + i.quantity, 0);
  const totalPrice = cart.items.reduce((sum, i) => sum + i.quantity * i.unitPrice, 0);

  return (
    <CartContext.Provider
      value={{
        businessId: cart.businessId,
        businessName: cart.businessName,
        items: cart.items,
        logisticsNote: cart.logisticsNote,
        isOpen,
        totalCount,
        totalPrice,
        wouldReplaceCart,
        inBasketQuantity,
        addItem,
        setQuantity,
        removeItem,
        clear,
        setLogisticsNote,
        open: () => setIsOpen(true),
        close: () => setIsOpen(false),
      }}
    >
      {children}
    </CartContext.Provider>
  );
}

export default CartProvider;
