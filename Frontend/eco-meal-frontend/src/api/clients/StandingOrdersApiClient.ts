import { http } from "../base/http";
import type { StandingOrderDto } from "../models/StandingOrder";

export const standingOrdersApi = {
  getMine: (): Promise<StandingOrderDto[]> => http.get<StandingOrderDto[]>("/standing-orders/mine"),

  create: (businessId: string, packageTypeId: string | null, dietaryTag: string | null, maxWeeklySpend: number): Promise<StandingOrderDto> =>
    http.post<StandingOrderDto>("/standing-orders", { businessId, packageTypeId, dietaryTag, maxWeeklySpend }),

  // No dedicated pause/resume route — PUT with the existing amount and a flipped IsActive does both.
  update: (id: string, maxWeeklySpend: number, isActive: boolean): Promise<void> =>
    http.put<void>(`/standing-orders/${id}`, { maxWeeklySpend, isActive }),

  remove: (id: string): Promise<void> => http.remove<void>(`/standing-orders/${id}`),
};
