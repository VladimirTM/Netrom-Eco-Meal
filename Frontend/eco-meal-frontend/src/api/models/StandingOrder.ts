export interface StandingOrderDto {
  id: string;
  businessId: string;
  businessName: string;
  packageTypeId: string | null;
  packageTypeName: string | null;
  dietaryTag: string | null;
  maxWeeklySpend: number;
  isActive: boolean;
  createdAt: string;
}
