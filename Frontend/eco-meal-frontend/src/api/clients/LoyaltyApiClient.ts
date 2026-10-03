import { http } from "../base/http";
import type { LoyaltyProgress } from "../models/Loyalty";

export const loyaltyApi = {
  getMyProgress: (businessId: string): Promise<LoyaltyProgress | null> => http.get<LoyaltyProgress | null>(`/loyalty/${businessId}/mine`),
};
