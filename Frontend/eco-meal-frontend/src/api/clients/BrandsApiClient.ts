import { http } from "../base/http";
import type { BrandDetailResponseDto } from "../models/Business";
import type { BrandDto } from "../models/Lookup";

export const brandsApi = {
  getAll: (): Promise<BrandDto[]> => http.get<BrandDto[]>("/brands"),

  getDetail: (id: string): Promise<BrandDetailResponseDto> => http.get<BrandDetailResponseDto>(`/brands/${id}`),

  getMyFavoriteIds: (): Promise<string[]> => http.get<string[]>("/brands/favorites/mine"),

  toggleFavorite: (id: string): Promise<boolean> => http.post<boolean>(`/brands/${id}/favorite`),
};
