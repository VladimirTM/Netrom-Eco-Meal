export interface NotificationDto {
  id: string;
  message: string;
  url: string | null;
  isRead: boolean;
  createdAt: string;
}
