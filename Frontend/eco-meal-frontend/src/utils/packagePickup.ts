// Mirrors BusinessDetail.razor's PickupLabel/ClosingSoonBadge — formats a package's UTC pickup
// window in the viewer's own time zone (see TimeZoneContext).

export function formatPickupWindow(pickupStartIso: string, pickupEndIso: string, timeZone: string): string {
  const start = new Date(pickupStartIso);
  const end = new Date(pickupEndIso);
  const datePart = new Intl.DateTimeFormat("en-US", { month: "short", day: "numeric", timeZone }).format(start);
  const timeFormat = new Intl.DateTimeFormat("ro-RO", { hour: "2-digit", minute: "2-digit", hourCycle: "h23", timeZone });
  return `${datePart} · ${timeFormat.format(start)}–${timeFormat.format(end)}`;
}

const CLOSING_SOON_THRESHOLD_MS = 60 * 60 * 1000;

export function closingSoonLabel(pickupEndIso: string, now: Date = new Date()): string | null {
  const remainingMs = new Date(pickupEndIso).getTime() - now.getTime();
  if (remainingMs <= 0 || remainingMs > CLOSING_SOON_THRESHOLD_MS) return null;

  const minutes = Math.max(1, Math.ceil(remainingMs / 60000));
  return `Ends in ${minutes} min`;
}
