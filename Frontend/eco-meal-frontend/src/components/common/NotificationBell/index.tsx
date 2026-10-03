import { useNotifications } from "../../../context/NotificationContext/notification-context";

interface NotificationBellProps {
  triggerClass?: string;
}

// Trigger only — the popup itself is NotificationPanel, rendered at the layout's top level so it
// can't get trapped in the header's sticky stacking context. See NotificationPanel's own comment.
function NotificationBell({ triggerClass = "notif-bell-btn" }: NotificationBellProps) {
  const { unreadCount, toggle } = useNotifications();

  return (
    <button type="button" className={triggerClass} title="Notifications" aria-label="Notifications" onClick={toggle}>
      <i className="bi bi-bell" />
      {unreadCount > 0 && <span className="notif-badge">{unreadCount > 9 ? "9+" : unreadCount}</span>}
    </button>
  );
}

export default NotificationBell;
