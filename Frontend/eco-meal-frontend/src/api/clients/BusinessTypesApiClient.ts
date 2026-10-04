import { http } from "../base/http";
import type { BusinessTypeDto } from "../models/Lookup";

export const businessTypesApi = {
  getAll: (): Promise<BusinessTypeDto[]> => http.get<BusinessTypeDto[]>("/business-types"),

  // Admin-only (BusinessTypeService enforces it); duplicate-name and "still in use" both arrive
  // as a 409 with body {"error": "..."} — Types.tsx shows that message verbatim.
  add: (name: string): Promise<BusinessTypeDto> => http.post<BusinessTypeDto>("/business-types", { name }),

  update: (id: string, name: string): Promise<void> => http.put<void>(`/business-types/${id}`, { name }),

  delete: (id: string): Promise<void> => http.remove<void>(`/business-types/${id}`),
};
