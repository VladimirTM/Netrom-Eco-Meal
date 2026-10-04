import { http } from "../base/http";
import type { BrandDetailResponseDto } from "../models/Business";
import type { BrandDto } from "../models/Lookup";

export const brandsApi = {
  getAll: (): Promise<BrandDto[]> => http.get<BrandDto[]>("/brands"),

  getDetail: (id: string): Promise<BrandDetailResponseDto> => http.get<BrandDetailResponseDto>(`/brands/${id}`),

  getMyFavoriteIds: (): Promise<string[]> => http.get<string[]>("/brands/favorites/mine"),

  toggleFavorite: (id: string): Promise<boolean> => http.post<boolean>(`/brands/${id}/favorite`),

  // Admin-only (BrandService enforces it); duplicate-name and "still in use" both arrive as a
  // 409 with body {"error": "..."} — Types.tsx shows that message verbatim.
  add: (name: string, description?: string | null): Promise<BrandDto> => http.post<BrandDto>("/brands", { name, description: description ?? null }),

  update: (id: string, name: string, description?: string | null): Promise<void> =>
    http.put<void>(`/brands/${id}`, { name, description: description ?? null }),

  delete: (id: string): Promise<void> => http.remove<void>(`/brands/${id}`),
};
