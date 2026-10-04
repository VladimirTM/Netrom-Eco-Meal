import { http } from "../base/http";
import type { ReportViewDto } from "../models/Report";

export const reportsApi = {
  submit: (targetType: string, targetId: string, reason: string): Promise<void> =>
    http.post<void>("/reports", { targetType, targetId, reason }),

  getOpen: (): Promise<ReportViewDto[]> => http.get<ReportViewDto[]>("/reports/open"),

  dismiss: (reportId: string): Promise<void> => http.post<void>(`/reports/${reportId}/dismiss`),

  takeAction: (reportId: string, actionReason: string): Promise<void> => http.post<void>(`/reports/${reportId}/take-action`, { actionReason }),
};
