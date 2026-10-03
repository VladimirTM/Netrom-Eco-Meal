import { http } from "../base/http";
import type { PackageDto } from "../models/Package";
import type { PaginatedList } from "../models/Pagination";

export const packagesApi = {
  getAll: (): Promise<PackageDto[]> => http.get<PackageDto[]>("/packages"),

  getPaged: (pageIndex: number, pageSize: number, businessId?: string): Promise<PaginatedList<PackageDto>> => {
    const search = new URLSearchParams({ pageIndex: String(pageIndex), pageSize: String(pageSize) });
    if (businessId) search.set("businessId", businessId);
    return http.get<PaginatedList<PackageDto>>(`/packages/paged?${search.toString()}`);
  },
};
