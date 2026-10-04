import { http } from "../base/http";
import type { PackageDto } from "../models/Package";
import type { PaginatedList } from "../models/Pagination";

// Matches PackageWriteDto exactly (D4 — status/moderation/donation fields are never settable here,
// they go through their own dedicated endpoints below).
export interface PackageWriteRequest {
  businessId: string;
  packageTypeId: string;
  name: string;
  description: string;
  price: number;
  quantity: number;
  weightKg: number;
  dietaryTags: string[];
  pickupStart: string;
  pickupEnd: string;
  imageUrl: string | null;
}

export interface MarkdownSuggestionDto {
  currentPrice: number;
  suggestedPrice: number;
  explanation: string;
}

export const packagesApi = {
  getAll: (): Promise<PackageDto[]> => http.get<PackageDto[]>("/packages"),

  getPaged: (pageIndex: number, pageSize: number, businessId?: string, search?: string, packageTypeId?: string): Promise<PaginatedList<PackageDto>> => {
    const search_ = new URLSearchParams({ pageIndex: String(pageIndex), pageSize: String(pageSize) });
    if (businessId) search_.set("businessId", businessId);
    if (search) search_.set("search", search);
    if (packageTypeId) search_.set("packageTypeId", packageTypeId);
    return http.get<PaginatedList<PackageDto>>(`/packages/paged?${search_.toString()}`);
  },

  getById: (id: string): Promise<PackageDto> => http.get<PackageDto>(`/packages/${id}`),

  create: (data: PackageWriteRequest): Promise<PackageDto> => http.post<PackageDto>("/packages", data),

  update: (id: string, data: PackageWriteRequest): Promise<void> => http.put<void>(`/packages/${id}`, data),

  remove: (id: string): Promise<void> => http.remove<void>(`/packages/${id}`),

  duplicateMany: (packageIds: string[]): Promise<PackageDto[]> => http.post<PackageDto[]>("/packages/bulk/duplicate", packageIds),

  adjustQuantityMany: (packageIds: string[], delta: number): Promise<void> =>
    http.post<void>("/packages/bulk/adjust-quantity", { packageIds, delta }),

  extendPickupWindowMany: (packageIds: string[], hours: number): Promise<void> =>
    http.post<void>("/packages/bulk/extend-pickup", { packageIds, hours }),

  getForAnalytics: (businessId: string | undefined, since: string): Promise<PackageDto[]> => {
    const search = new URLSearchParams({ since });
    if (businessId) search.set("businessId", businessId);
    return http.get<PackageDto[]>(`/packages/analytics?${search.toString()}`);
  },

  getMarkdownCandidates: (businessId?: string): Promise<PackageDto[]> =>
    http.get<PackageDto[]>(`/packages/markdown-candidates${businessId ? `?businessId=${businessId}` : ""}`),

  getMarkdownSuggestion: (id: string): Promise<MarkdownSuggestionDto | null> =>
    http.get<MarkdownSuggestionDto | null>(`/packages/${id}/markdown-suggestion`),

  dismissMarkdownSuggestion: (id: string): Promise<void> => http.post<void>(`/packages/${id}/markdown-suggestion/dismiss`),

  getDonationCandidates: (businessId?: string): Promise<PackageDto[]> =>
    http.get<PackageDto[]>(`/packages/donation-candidates${businessId ? `?businessId=${businessId}` : ""}`),

  markAsDonated: (id: string): Promise<PackageDto> => http.post<PackageDto>(`/packages/${id}/donate`),

  hide: (id: string, reason: string): Promise<void> => http.post<void>(`/packages/${id}/hide`, { reason }),

  unhide: (id: string): Promise<void> => http.post<void>(`/packages/${id}/unhide`),
};
