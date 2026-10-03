import { http } from "../base/http";

export const favoritesApi = {
  getMine: (): Promise<string[]> => http.get<string[]>("/favorites/mine"),

  toggle: (businessId: string): Promise<boolean> => http.post<boolean>(`/favorites/${businessId}/toggle`),
};
