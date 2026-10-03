import { http } from "../base/http";
import type { ReviewContextDto, ReviewDto } from "../models/Package";

export const reviewsApi = {
  getByBusiness: (businessId: string): Promise<ReviewDto[]> => http.get<ReviewDto[]>(`/reviews/by-business/${businessId}`),

  getByBusinesses: (businessIds: string[]): Promise<ReviewDto[]> => {
    if (businessIds.length === 0) return Promise.resolve([]);
    const search = new URLSearchParams();
    businessIds.forEach((id) => search.append("businessIds", id));
    return http.get<ReviewDto[]>(`/reviews/by-businesses?${search.toString()}`);
  },

  getContext: (businessId: string): Promise<ReviewContextDto> => http.get<ReviewContextDto>(`/reviews/context/${businessId}`),

  submit: (businessId: string, rating: number, comment: string | null, packageId: string | null): Promise<ReviewDto> =>
    http.post<ReviewDto>(`/reviews/${businessId}`, { rating, comment, packageId }),
};
