import { http } from "../base/http";
import type { BusinessDto } from "../models/Business";
import type { PaginatedList } from "../models/Pagination";

export interface GetPagedBusinessesParams {
  pageIndex?: number;
  pageSize?: number;
  search?: string;
  businessTypeId?: string;
  sortBy?: string;
  favoritesOnly?: boolean;
  customerLat?: number;
  customerLng?: number;
  publicOnly?: boolean;
  dietaryTag?: string;
  maxPrice?: number;
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

  apply: (data: BusinessApplyRequest): Promise<BusinessDto> => http.post<BusinessDto>("/businesses/apply", data),
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
