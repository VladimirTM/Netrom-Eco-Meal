// Shared request-line shape for every endpoint that starts a checkout from basket lines
// (PlaceOrderRequestDto/StartCheckoutRequestDto/StartRescueCircleRequestDto all take this).
export interface OrderLineRequest {
  packageId: string;
  quantity: number;
}

// WeightKg lets the frontend compute Orders.razor's own "kg saved" formula (Completed orders'
// Sum(Quantity * WeightKg)); PickupStart/PickupEnd let it compute the order-level pickup-window
// span the same way — both without a dedicated endpoint.
export interface OrderLineDto {
  packageId: string;
  packageName: string;
  quantity: number;
  unitPrice: number;
  weightKg: number;
  pickupStart: string;
  pickupEnd: string;
}

export interface PickupPassDto {
  id: string;
  label: string;
  createdAt: string;
  redeemedAt: string | null;
}

export interface PaymentDto {
  id: string;
  amount: number;
  currency: string;
  status: string;
  createdAt: string;
  refundedAt: string | null;
}

export type OrderStatusName = "Pending" | "Confirmed" | "Completed" | "Cancelled" | "NoShow";

export interface OrderDto {
  id: string;
  orderNumber: number;
  businessId: string;
  businessName: string;
  userId: string;
  customerName: string;
  status: OrderStatusName;
  createdAt: string;
  logisticsNote: string | null;
  lines: OrderLineDto[];
  total: number;
  pickupPasses: PickupPassDto[];
  payment: PaymentDto | null;
}
