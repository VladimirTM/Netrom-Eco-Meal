import { http } from "../base/http";
import type { OrderDto } from "../models/Order";
import type { PaginatedList } from "../models/Pagination";

export const ordersApi = {
  // Anonymous, read-only endpoints for the browsing pages.
  getPendingReservedQuantities: (packageIds: string[]): Promise<Record<string, number>> => {
    if (packageIds.length === 0) return Promise.resolve({});
    const search = new URLSearchParams();
    packageIds.forEach((id) => search.append("packageIds", id));
    return http.get<Record<string, number>>(`/orders/reserved-quantities?${search.toString()}`);
  },

  getTotalKgSaved: (): Promise<number> => http.get<number>("/orders/impact/kg-saved"),

  // A customer's own orders.
  getMine: (): Promise<OrderDto[]> => http.get<OrderDto[]>("/orders/mine"),

  getMinePaged: (pageIndex: number, pageSize: number, status: string | null): Promise<PaginatedList<OrderDto>> => {
    const search = new URLSearchParams({ pageIndex: String(pageIndex), pageSize: String(pageSize) });
    if (status) search.set("status", status);
    return http.get<PaginatedList<OrderDto>>(`/orders/mine/paged?${search.toString()}`);
  },

  getMineById: (orderId: string): Promise<OrderDto> => http.get<OrderDto>(`/orders/mine/${orderId}`),

  cancel: (orderId: string): Promise<OrderDto> => http.post<OrderDto>(`/orders/${orderId}/cancel`),

  splitPickupPasses: (orderId: string, passCount: number): Promise<OrderDto> =>
    http.post<OrderDto>(`/orders/${orderId}/split-passes`, { passCount }),

  redeemPickupPass: (orderId: string, passId: string): Promise<OrderDto> =>
    http.post<OrderDto>(`/orders/${orderId}/passes/${passId}/redeem`),
};
