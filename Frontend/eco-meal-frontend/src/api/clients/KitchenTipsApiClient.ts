import { http } from "../base/http";
import type { KitchenTipDto } from "../models/Package";

export const kitchenTipsApi = {
  getByBusiness: (businessId: string): Promise<KitchenTipDto[]> => http.get<KitchenTipDto[]>(`/kitchen-tips/by-business/${businessId}`),

  submit: (businessId: string, tip: string): Promise<KitchenTipDto> => http.post<KitchenTipDto>(`/kitchen-tips/${businessId}`, { tip }),
};
