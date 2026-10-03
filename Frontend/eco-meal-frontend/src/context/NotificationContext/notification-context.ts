import { createContext, useContext } from "react";
import type { NotificationDto } from "../../api/models/Notification";

export interface NotificationContextValue {
  unreadCount: number;
  // null while the panel's own list is loading (panel shows "Loading…") — distinct from an
  // empty array, which means "loaded, nothing there".
  notifications: NotificationDto[] | null;
  isOpen: boolean;
  toggle: () => void;
  close: () => void;
  markAllRead: () => void;
  markAsRead: (notification: NotificationDto) => void;
}

export const NotificationContext = createContext<NotificationContextValue | null>(null);

export function useNotifications(): NotificationContextValue {
  const ctx = useContext(NotificationContext);
  if (!ctx) throw new Error("useNotifications must be used inside NotificationProvider");
  return ctx;
}
