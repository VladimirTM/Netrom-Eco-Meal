export type RescueCircleStatusName = "Open" | "Cancelled";

export interface RescueCircleSummary {
  id: string;
  businessId: string;
  businessName: string;
  status: RescueCircleStatusName;
  participantCount: number;
  joinedCount: number;
  paidCount: number;
  totalAmount: number;
  shareAmount: number;
  isMine: boolean;
}

export interface RescueCircleParticipantDto {
  userId: string;
  userName: string;
  isOrganizer: boolean;
  shareAmount: number;
  joinedAt: string;
  paidAt: string | null;
  refundedAt: string | null;
}

export interface RescueCircleDetailDto {
  summary: RescueCircleSummary;
  organizerId: string;
  participants: RescueCircleParticipantDto[];
}

export interface RescueCircleCompletionResponseDto {
  success: boolean;
  message: string;
  summary: RescueCircleSummary | null;
}
