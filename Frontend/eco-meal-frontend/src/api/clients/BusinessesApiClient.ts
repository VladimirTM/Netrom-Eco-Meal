import { http } from "../base/http";
import type { BusinessClosureDto, BusinessHoursDto, BusinessDto } from "../models/Business";
import type { PaginatedList } from "../models/Pagination";

export interface GetPagedBusinessesParams {
  pageIndex?: number;
  pageSize?: number;
  search?: string;
  businessTypeId?: string;
  staffUserId?: string;
  sortBy?: string;
  favoritesOnly?: boolean;
  customerLat?: number;
  customerLng?: number;
  statusFilter?: string;
  publicOnly?: boolean;
  dietaryTag?: string;
  maxPrice?: number;
}

export interface StaffMemberDto {
  id: string;
  name: string;
  email: string;
}

// Shared by create/apply (POST) and update (PUT) — matches BusinessWriteDto exactly (D4: status/
// approval/webhook-key fields are never settable from this body, they go through their own routes).
export interface BusinessWriteRequest {
  name: string;
  description: string;
  address: string;
  imageUrl: string | null;
  latitude: number | null;
  longitude: number | null;
  businessTypeId: string;
  brandId: string | null;
  loyaltyPunchThreshold: number | null;
  loyaltyDiscountAmount: number | null;
}

function toQuery(params: GetPagedBusinessesParams): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== "") search.set(key, String(value));
  }
  const query = search.toString();
  return query ? `?${query}` : "";
}

export const businessesApi = {
  getPaged: (params: GetPagedBusinessesParams): Promise<PaginatedList<BusinessDto>> =>
    http.get<PaginatedList<BusinessDto>>(`/businesses${toQuery(params)}`),

  getAll: (publicOnly = false): Promise<BusinessDto[]> => http.get<BusinessDto[]>(`/businesses/all${toQuery({ publicOnly })}`),

  getById: (id: string): Promise<BusinessDto> => http.get<BusinessDto>(`/businesses/${id}`),

  getStaff: (id: string): Promise<StaffMemberDto[]> => http.get<StaffMemberDto[]>(`/businesses/${id}/staff`),

  apply: (data: BusinessApplyRequest): Promise<BusinessDto> => http.post<BusinessDto>("/businesses/apply", data),

  // Admin-only (BusinessService.AddAsync enforces it) — unlike apply(), the business is created
  // straight into Approved status, no admin review step. BusinessForm.razor's "Create Business" button.
  create: (data: BusinessWriteRequest): Promise<BusinessDto> => http.post<BusinessDto>("/businesses", data),

  update: (id: string, data: BusinessWriteRequest): Promise<void> => http.put<void>(`/businesses/${id}`, data),

  setHours: (id: string, hours: BusinessHoursDto[]): Promise<void> => http.put<void>(`/businesses/${id}/hours`, { hours }),

  addClosure: (id: string, startDate: string, endDate: string, reason: string | null): Promise<BusinessClosureDto> =>
    http.post<BusinessClosureDto>(`/businesses/${id}/closures`, { startDate, endDate, reason }),

  removeClosure: (id: string, closureId: string): Promise<void> => http.remove<void>(`/businesses/${id}/closures/${closureId}`),

  // Returns the plaintext key — shown to the manager exactly once, same contract as Blazor's Dashboard.razor.
  generateApiKey: (id: string): Promise<string> => http.post<string>(`/businesses/${id}/webhook-key`),

  revokeApiKey: (id: string): Promise<void> => http.remove<void>(`/businesses/${id}/webhook-key`),

  // Admin moderation — enforced server-side by BusinessesController's [Authorize(Roles = Admin)].
  delete: (id: string): Promise<void> => http.remove<void>(`/businesses/${id}`),

  approve: (id: string): Promise<void> => http.post<void>(`/businesses/${id}/approve`),

  reject: (id: string, reason: string): Promise<void> => http.post<void>(`/businesses/${id}/reject`, { reason }),

  hide: (id: string, reason: string): Promise<void> => http.post<void>(`/businesses/${id}/hide`, { reason }),

  unhide: (id: string): Promise<void> => http.post<void>(`/businesses/${id}/unhide`),

  // Returns 204, or a bare 409 (no body) if the assignment already exists — the caller checks
  // `err.status === 409` on the rejected ApiError, matching Businesses.razor's own hardcoded
  // "That staff assignment already exists." message (there's no server-provided text for this one).
  addStaff: (id: string, userId: string, userName?: string): Promise<void> => http.post<void>(`/businesses/${id}/staff`, { userId, userName }),

  removeStaff: (id: string, userId: string): Promise<void> => http.remove<void>(`/businesses/${id}/staff/${userId}`),
};

export interface BusinessApplyRequest {
  name: string;
  description: string;
  address: string;
  imageUrl: string | null;
  latitude: number | null;
  longitude: number | null;
  businessTypeId: string;
  brandId: string | null;
  loyaltyPunchThreshold: number | null;
  loyaltyDiscountAmount: number | null;
}
