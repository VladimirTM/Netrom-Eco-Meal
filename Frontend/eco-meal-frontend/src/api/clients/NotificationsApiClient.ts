import { http } from "../base/http";
import type { NotificationDto } from "../models/Notification";

export const notificationsApi = {
  getMine: (take = 20): Promise<NotificationDto[]> => http.get<NotificationDto[]>(`/notifications/mine?take=${take}`),

  getUnreadCount: (): Promise<number> => http.get<number>("/notifications/mine/unread-count"),

  markAsRead: (id: string): Promise<void> => http.post<void>(`/notifications/${id}/read`),

  markAllAsRead: (): Promise<void> => http.post<void>("/notifications/mine/read-all"),
};
