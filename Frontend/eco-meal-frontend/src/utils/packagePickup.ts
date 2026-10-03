// Mirrors BusinessDetail.razor's PickupLabel/ClosingSoonBadge — formats a package's UTC pickup
// window in the viewer's own time zone (see TimeZoneContext).

export function formatPickupWindow(pickupStartIso: string, pickupEndIso: string, timeZone: string): string {
  const start = new Date(pickupStartIso);
  const end = new Date(pickupEndIso);
  const datePart = new Intl.DateTimeFormat("en-US", { month: "short", day: "numeric", timeZone }).format(start);
  const timeFormat = new Intl.DateTimeFormat("ro-RO", { hour: "2-digit", minute: "2-digit", hourCycle: "h23", timeZone });
  return `${datePart} · ${timeFormat.format(start)}–${timeFormat.format(end)}`;
}

// Mirrors Orders.razor/OrderPickupPass.razor's own PickupWindow — an order can span packages
// with different pickup windows, so this shows the widest span (earliest start, latest end)
// rather than any single package's own window.
export function formatPickupRange(pickupStartIso: string, pickupEndIso: string, timeZone: string): string {
  const start = new Date(pickupStartIso);
  const end = new Date(pickupEndIso);
  const dateFormat = new Intl.DateTimeFormat("en-US", { month: "short", day: "numeric", timeZone });
  const timeFormat = new Intl.DateTimeFormat("ro-RO", { hour: "2-digit", minute: "2-digit", hourCycle: "h23", timeZone });

  const startDateKey = new Intl.DateTimeFormat("en-US", { year: "numeric", month: "numeric", day: "numeric", timeZone }).format(start);
  const endDateKey = new Intl.DateTimeFormat("en-US", { year: "numeric", month: "numeric", day: "numeric", timeZone }).format(end);

  return startDateKey === endDateKey
    ? `${dateFormat.format(start)} · ${timeFormat.format(start)}–${timeFormat.format(end)}`
    : `${dateFormat.format(start)}, ${timeFormat.format(start)} – ${dateFormat.format(end)}, ${timeFormat.format(end)}`;
}

// An order can span packages with different pickup windows — shows the widest span (earliest
// start, latest end) across all of its lines, same as Orders.razor/OrderPickupPass.razor's own
// PickupWindow helper.
export function formatOrderPickupWindow(lines: { pickupStart: string; pickupEnd: string }[], timeZone: string): string {
  if (lines.length === 0) return "time to be confirmed";
  const start = lines.reduce((min, l) => (l.pickupStart < min ? l.pickupStart : min), lines[0].pickupStart);
  const end = lines.reduce((max, l) => (l.pickupEnd > max ? l.pickupEnd : max), lines[0].pickupEnd);
  return formatPickupRange(start, end, timeZone);
}

const CLOSING_SOON_THRESHOLD_MS = 60 * 60 * 1000;

export function closingSoonLabel(pickupEndIso: string, now: Date = new Date()): string | null {
  const remainingMs = new Date(pickupEndIso).getTime() - now.getTime();
  if (remainingMs <= 0 || remainingMs > CLOSING_SOON_THRESHOLD_MS) return null;

  const minutes = Math.max(1, Math.ceil(remainingMs / 60000));
  return `Ends in ${minutes} min`;
}
