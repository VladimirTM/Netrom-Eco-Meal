import { http } from "../base/http";
import type { PackageTemplateDto } from "../models/PackageTemplate";

export const packageTemplatesApi = {
  getAll: (): Promise<PackageTemplateDto[]> => http.get<PackageTemplateDto[]>("/package-templates"),

  getByBusiness: (businessId: string): Promise<PackageTemplateDto[]> =>
    http.get<PackageTemplateDto[]>(`/package-templates/by-business/${businessId}`),

  create: (packageId: string, pickupStartTimeUtc: string, pickupEndTimeUtc: string): Promise<PackageTemplateDto> =>
    http.post<PackageTemplateDto>("/package-templates", { packageId, pickupStartTimeUtc, pickupEndTimeUtc }),

  setActive: (id: string, isActive: boolean): Promise<void> => http.put<void>(`/package-templates/${id}/active`, { isActive }),

  remove: (id: string): Promise<void> => http.remove<void>(`/package-templates/${id}`),
};
