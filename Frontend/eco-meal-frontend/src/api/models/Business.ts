// Matches System.DayOfWeek's enum member names — the backend serializes it as a string via the
// global JsonStringEnumConverter (Program.cs), not as a 0–6 index.
export type DayOfWeekName = "Sunday" | "Monday" | "Tuesday" | "Wednesday" | "Thursday" | "Friday" | "Saturday";

export interface BusinessHoursDto {
  dayOfWeek: DayOfWeekName;
  isClosed: boolean;
  // TimeOnly serializes as "HH:mm:ss" (or with a fractional-seconds suffix) — never null unless isClosed.
  openTime: string | null;
  closeTime: string | null;
}

export interface BusinessClosureDto {
  id: string;
  // DateOnly serializes as "yyyy-MM-dd".
  startDate: string;
  endDate: string;
  reason: string | null;
}

export interface BusinessDto {
  id: string;
  name: string;
  description: string;
  address: string;
  imageUrl: string | null;
  latitude: number | null;
  longitude: number | null;
  status: string;
  rejectionReason: string | null;
  isHidden: boolean;
  hiddenReason: string | null;
  loyaltyPunchThreshold: number | null;
  loyaltyDiscountAmount: number | null;
  businessTypeId: string;
  businessTypeName: string;
  brandId: string | null;
  brandName: string | null;
  hasWebhookApiKey: boolean;
  webhookApiKeyLastUsedAt: string | null;
  hours: BusinessHoursDto[];
  closures: BusinessClosureDto[];
}

export interface BrandDetailResponseDto {
  brand: { id: string; name: string; description: string | null };
  locations: BusinessDto[];
  averageRating: number | null;
  ratingCount: number;
}
