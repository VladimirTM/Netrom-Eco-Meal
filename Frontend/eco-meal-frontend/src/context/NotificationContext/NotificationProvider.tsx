import { useCallback, useEffect, useLayoutEffect, useRef, useState, type ReactNode } from "react";
import { notificationsApi } from "../../api/clients/NotificationsApiClient";
import type { NotificationDto } from "../../api/models/Notification";
import { useAuth } from "../AuthContext/auth-context";
import { NotificationContext } from "./notification-context";

const POLL_INTERVAL_MS = 30_000;

// Mirrors NotificationPanelState (Backend/NetromEcoMeal.Web/Services/NotificationPanelState.cs) —
// shared between the trigger button (header) and the popup panel, which has to render outside the
// header's own stacking context (see NotificationPanel's comment), so they can't just be
// parent/child state. Only polls while signed in — an anonymous visitor has no bell to open it with.
function NotificationProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth();
  const [unreadCount, setUnreadCount] = useState(0);
  const [notifications, setNotifications] = useState<NotificationDto[] | null>(null);
  const [isOpen, setIsOpen] = useState(false);

  const refreshUnreadCount = useCallback(async () => {
    try {
      setUnreadCount(await notificationsApi.getUnreadCount());
    } catch {
      // Best-effort poll — the next tick retries. Nothing to surface a failure to here, same
      // "a raw timer callback must never throw" rule the Blazor version documents.
    }
  }, []);

  const refreshRef = useRef(refreshUnreadCount);
  useLayoutEffect(() => {
    refreshRef.current = refreshUnreadCount;
  });

  // Resets to the signed-out defaults the instant isAuthenticated flips, without a useEffect
  // round trip — same "adjusting state when a prop changes" pattern PackageDetailModal uses.
  const [wasAuthenticated, setWasAuthenticated] = useState(isAuthenticated);
  if (isAuthenticated !== wasAuthenticated) {
    setWasAuthenticated(isAuthenticated);
    if (!isAuthenticated) {
      setUnreadCount(0);
      setIsOpen(false);
      setNotifications(null);
    }
  }

  useEffect(() => {
    if (!isAuthenticated) return;
    void refreshRef.current();
    const timer = setInterval(() => void refreshRef.current(), POLL_INTERVAL_MS);
    return () => clearInterval(timer);
  }, [isAuthenticated]);

  function toggle() {
    setIsOpen((prev) => {
      const next = !prev;
      if (next) {
        setNotifications(null);
        notificationsApi
          .getMine(20)
          .then(setNotifications)
          .catch(() => setNotifications([]));
      }
      return next;
    });
  }

  function close() {
    setIsOpen(false);
  }

  async function markAllRead() {
    await notificationsApi.markAllAsRead();
    setNotifications((prev) => prev?.map((n) => ({ ...n, isRead: true })) ?? prev);
    setUnreadCount(0);
  }

  async function markAsRead(notification: NotificationDto) {
    if (notification.isRead) return;
    await notificationsApi.markAsRead(notification.id);
    setNotifications((prev) => prev?.map((n) => (n.id === notification.id ? { ...n, isRead: true } : n)) ?? prev);
    setUnreadCount((prev) => Math.max(0, prev - 1));
  }

  return (
    <NotificationContext.Provider value={{ unreadCount, notifications, isOpen, toggle, close, markAllRead, markAsRead }}>
      {children}
    </NotificationContext.Provider>
  );
}

export default NotificationProvider;
