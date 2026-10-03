import { http } from "../base/http";
import type { OrderLineRequest } from "../models/Order";
import type { CheckoutCompletionResponseDto } from "../models/Payment";

export const paymentsApi = {
  // Returns the Stripe Checkout URL to redirect the browser to.
  createCheckoutSession: (businessId: string, lines: OrderLineRequest[], logisticsNote: string | null): Promise<string> =>
    http.post<string>("/payments/checkout-session", { businessId, lines, logisticsNote }),

  complete: (pendingCheckoutId: string, sessionId: string): Promise<CheckoutCompletionResponseDto> =>
    http.post<CheckoutCompletionResponseDto>("/payments/complete", { pendingCheckoutId, sessionId }),
};
