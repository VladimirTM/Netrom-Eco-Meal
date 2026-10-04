import { createContext, useContext } from "react";
import type { BusinessDto } from "../../api/models/Business";

export interface ManagedBusinessContextValue {
  myBusinesses: BusinessDto[];
  selectedBusinessId: string | null;
  loading: boolean;
  select: (businessId: string) => void;
}

export const ManagedBusinessContext = createContext<ManagedBusinessContextValue | null>(null);

export function useManagedBusiness(): ManagedBusinessContextValue {
  const ctx = useContext(ManagedBusinessContext);
  if (!ctx) throw new Error("useManagedBusiness must be used inside ManagedBusinessProvider");
  return ctx;
}
