import { createContext, useContext } from "react";

export interface CartLineItem {
  packageId: string;
  name: string;
  unitPrice: number;
  quantity: number;
  // Package.Quantity at the moment it was added/last refreshed — clamps the line's own stepper,
  // same as CartService.Items' Package navigation did in Blazor.
  maxQuantity: number;
}

export interface AddToCartPackage {
  id: string;
  name: string;
  price: number;
  quantity: number;
}

export interface CartContextValue {
  businessId: string | null;
  businessName: string | null;
  items: CartLineItem[];
  logisticsNote: string;
  isOpen: boolean;
  totalCount: number;
  totalPrice: number;

  // Pure predicate — callers show a ConfirmDialog ("Start a new basket?") before calling addItem
  // when this is true, same as BusinessDetail.razor/Orders.razor's own call sites.
  wouldReplaceCart: (businessId: string) => boolean;
  inBasketQuantity: (packageId: string) => number;

  addItem: (businessId: string, businessName: string, pkg: AddToCartPackage, quantity: number) => void;
  setQuantity: (packageId: string, quantity: number) => void;
  removeItem: (packageId: string) => void;
  clear: () => void;
  setLogisticsNote: (note: string) => void;
  open: () => void;
  close: () => void;
}

export const CartContext = createContext<CartContextValue | null>(null);

export function useCart(): CartContextValue {
  const ctx = useContext(CartContext);
  if (!ctx) throw new Error("useCart must be used inside CartProvider");
  return ctx;
}
