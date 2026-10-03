import type { PackageDto } from "./Package";

// Mirrors NetromEcoMeal.Models.SearchIntent — fed back into searchIntent.parse as refinement
// context so a follow-up like "cheaper" adjusts this instead of starting over.
export interface SearchIntent {
  keywords: string | null;
  dietaryTag: string | null;
  maxPrice: number | null;
  closingSoon: boolean;
  nearMe: boolean;
}

export interface BasketPlanItemDto {
  package: PackageDto;
  quantity: number;
  reason: string;
  lineTotal: number;
}

export interface BasketPlanDto {
  items: BasketPlanItemDto[];
  totalPrice: number;
  explanation: string;
}
