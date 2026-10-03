import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { pushSubscriptionsApi } from "../../../api/clients/PushSubscriptionsApiClient";
import type { NotificationDto } from "../../../api/models/Notification";
import { useAuth } from "../../../context/AuthContext/auth-context";
import { useNotifications } from "../../../context/NotificationContext/notification-context";
import { isPushSupported, usePushSubscription } from "../../../hooks/usePushSubscription";

function relativeTime(createdAtIso: string): string {
  const spanMs = Date.now() - new Date(createdAtIso).getTime();
  const minutes = spanMs / 60_000;
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${Math.floor(minutes)}m ago`;
  const hours = minutes / 60;
  if (hours < 24) return `${Math.floor(hours)}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
}

// Rendered once at the layout's top level, not inside NotificationBell — a trigger nested in
// .public-header traps a fixed popup under <main> (sticky ancestors always create a stacking
// context). Centered like ConfirmDialog/ReportDialog, which don't have this problem.
function NotificationPanel() {
  const { isOpen, unreadCount, notifications, close, markAllRead, markAsRead } = useNotifications();
  const { isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const push = usePushSubscription();

  const [pushPublicKey, setPushPublicKey] = useState<string | null>(null);
  const [pushEndpoint, setPushEndpoint] = useState<string | null>(null);
  const [pushBusy, setPushBusy] = useState(false);
  const [pushError, setPushError] = useState<string | null>(null);

  useEffect(() => {
    if (!isAuthenticated) return;
    let cancelled = false;

    (async () => {
      // Null when WebPush isn't configured server-side — the toggle stays hidden entirely, same
      // degrade-gracefully pattern as a missing Stripe/SMTP config elsewhere in the app.
      const publicKey = await pushSubscriptionsApi.getPublicKey().catch(() => null);
      if (cancelled || !publicKey || !isPushSupported()) return;
      setPushPublicKey(publicKey);
      const endpoint = await push.getSubscriptionEndpoint();
      if (!cancelled) setPushEndpoint(endpoint);
    })();

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated]);

  async function togglePush() {
    setPushBusy(true);
    setPushError(null);

    try {
      if (pushEndpoint) {
        const unsubscribedEndpoint = await push.unsubscribe();
        if (unsubscribedEndpoint) await pushSubscriptionsApi.unsubscribe(unsubscribedEndpoint);
        setPushEndpoint(null);
      } else if (pushPublicKey) {
        const subscription = await push.subscribe(pushPublicKey);
        if (!subscription) {
          setPushError("Couldn't enable browser alerts — check your browser's notification permission.");
        } else {
          await pushSubscriptionsApi.subscribe(subscription.endpoint, subscription.p256dh, subscription.auth);
          setPushEndpoint(subscription.endpoint);
        }
      }
    } finally {
      setPushBusy(false);
    }
  }

  function open(notification: NotificationDto) {
    markAsRead(notification);
    close();
    if (notification.url) navigate(notification.url);
  }

  if (!isOpen) return null;

  return (
    <>
      <div className="notif-panel-backdrop" onClick={close} />
      <div className="notif-panel" role="dialog" aria-modal="true" aria-label="Notifications">
        <div className="notif-panel-header">
          <span>Notifications</span>
          <div className="notif-panel-header-actions">
            {pushPublicKey && isPushSupported() && (
              <button
                type="button"
                className={`notif-push-toggle ${pushEndpoint ? "notif-push-toggle-on" : ""}`}
                title={pushEndpoint ? "Disable browser alerts" : "Enable browser alerts"}
                disabled={pushBusy}
                onClick={() => void togglePush()}
              >
                <i className={`bi ${pushEndpoint ? "bi-bell-fill" : "bi-bell-slash"}`} />
              </button>
            )}
            {unreadCount > 0 && (
              <button type="button" className="notif-mark-all" onClick={() => void markAllRead()}>
                Mark all read
              </button>
            )}
            <button type="button" className="notif-panel-close" title="Close" aria-label="Close" onClick={close}>
              <i className="bi bi-x-lg" />
            </button>
          </div>
        </div>

        {pushError && <div className="notif-push-error">{pushError}</div>}

        <div className="notif-panel-list">
          {notifications === null ? (
            <div className="notif-empty">Loading…</div>
          ) : notifications.length === 0 ? (
            <div className="notif-empty">
              <i className="bi bi-check2-circle" />
              You&apos;re all caught up.
            </div>
          ) : (
            notifications.map((notification) => (
              <button
                type="button"
                className={`notif-item ${notification.isRead ? "" : "notif-item-unread"}`}
                key={notification.id}
                onClick={() => open(notification)}
              >
                <span className="notif-item-message">{notification.message}</span>
                <span className="notif-item-time">{relativeTime(notification.createdAt)}</span>
              </button>
            ))
          )}
        </div>
      </div>
    </>
  );
}

export default NotificationPanel;
