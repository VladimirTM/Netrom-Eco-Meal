export interface PackageTemplateDto {
  id: string;
  businessId: string;
  packageTypeId: string;
  packageTypeName: string;
  name: string;
  description: string;
  price: number;
  quantity: number;
  weightKg: number;
  dietaryTags: string[];
  // TimeSpan serializes as "HH:mm:ss" — a daily time-of-day, not a UTC instant, so it's not run
  // through utils/dates.ts's timezone conversion (see PickupStart/PickupEnd there for contrast).
  pickupStartTimeUtc: string;
  pickupEndTimeUtc: string;
  imageUrl: string | null;
  isActive: boolean;
  lastGeneratedDate: string | null;
}
