import { http } from "../base/http";

// Only the two anonymous, read-only order endpoints Phase 5's browsing pages need — placing/
// managing orders is Phase 6's OrdersApiClient.
export const ordersApi = {
  getPendingReservedQuantities: (packageIds: string[]): Promise<Record<string, number>> => {
    if (packageIds.length === 0) return Promise.resolve({});
    const search = new URLSearchParams();
    packageIds.forEach((id) => search.append("packageIds", id));
    return http.get<Record<string, number>>(`/orders/reserved-quantities?${search.toString()}`);
  },

  getTotalKgSaved: (): Promise<number> => http.get<number>("/orders/impact/kg-saved"),
};
