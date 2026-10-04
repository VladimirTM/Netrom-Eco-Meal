import { http } from "../base/http";
import type { PackageTypeDto } from "../models/Lookup";

export const packageTypesApi = {
  getAll: (): Promise<PackageTypeDto[]> => http.get<PackageTypeDto[]>("/package-types"),

  // Admin-only (PackageTypeService enforces it); duplicate-name and "still in use" both arrive
  // as a 409 with body {"error": "..."} — Types.tsx shows that message verbatim.
  add: (name: string): Promise<PackageTypeDto> => http.post<PackageTypeDto>("/package-types", { name }),

  update: (id: string, name: string): Promise<void> => http.put<void>(`/package-types/${id}`, { name }),

  delete: (id: string): Promise<void> => http.remove<void>(`/package-types/${id}`),
};
