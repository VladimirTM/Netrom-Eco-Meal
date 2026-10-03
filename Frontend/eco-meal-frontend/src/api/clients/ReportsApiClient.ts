import { http } from "../base/http";

export const reportsApi = {
  submit: (targetType: string, targetId: string, reason: string): Promise<void> =>
    http.post<void>("/reports", { targetType, targetId, reason }),
};
