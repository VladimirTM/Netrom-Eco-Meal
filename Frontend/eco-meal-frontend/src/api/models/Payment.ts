import type { OrderDto } from "./Order";

export interface CheckoutCompletionResponseDto {
  success: boolean;
  message: string;
  order: OrderDto | null;
  kgSaved: number | null;
}
