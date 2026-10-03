import { http } from "../base/http";
import type { OrderLineRequest } from "../models/Order";
import type { RescueCircleCompletionResponseDto, RescueCircleDetailDto, RescueCircleSummary } from "../models/RescueCircle";

export const rescueCirclesApi = {
  // Returns the organizer's own Stripe Checkout URL (their share).
  startCircle: (businessId: string, lines: OrderLineRequest[], participantCount: number, logisticsNote: string | null): Promise<string> =>
    http.post<string>("/rescue-circles", { businessId, lines, participantCount, logisticsNote }),

  joinOrPay: (circleId: string): Promise<string> => http.post<string>(`/rescue-circles/${circleId}/join-or-pay`),

  completeShareCheckout: (circleId: string, participantId: string, sessionId: string): Promise<RescueCircleCompletionResponseDto> =>
    http.post<RescueCircleCompletionResponseDto>(`/rescue-circles/${circleId}/complete-share`, { participantId, sessionId }),

  getSummary: (circleId: string): Promise<RescueCircleSummary> => http.get<RescueCircleSummary>(`/rescue-circles/${circleId}/summary`),

  getMine: (): Promise<RescueCircleSummary[]> => http.get<RescueCircleSummary[]>("/rescue-circles/mine"),

  getDetail: (circleId: string): Promise<RescueCircleDetailDto> => http.get<RescueCircleDetailDto>(`/rescue-circles/${circleId}`),

  leave: (circleId: string): Promise<void> => http.post<void>(`/rescue-circles/${circleId}/leave`),
};
