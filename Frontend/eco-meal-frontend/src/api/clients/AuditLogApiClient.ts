import { http } from "../base/http";
import type { AuditLogDto } from "../models/AuditLog";
import type { PaginatedList } from "../models/Pagination";

export const auditLogApi = {
  getPaged: (pageIndex = 1, pageSize = 20, action?: string, targetType?: string, search?: string): Promise<PaginatedList<AuditLogDto>> => {
    const query = new URLSearchParams({ pageIndex: String(pageIndex), pageSize: String(pageSize) });
    if (action) query.set("action", action);
    if (targetType) query.set("targetType", targetType);
    if (search) query.set("search", search);
    return http.get<PaginatedList<AuditLogDto>>(`/audit-log?${query.toString()}`);
  },
};
