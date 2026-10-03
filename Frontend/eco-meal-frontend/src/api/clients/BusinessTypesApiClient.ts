import { http } from "../base/http";
import type { BusinessTypeDto } from "../models/Lookup";

export const businessTypesApi = {
  getAll: (): Promise<BusinessTypeDto[]> => http.get<BusinessTypeDto[]>("/business-types"),
};
