import type { BusinessClosureDto, BusinessHoursDto, DayOfWeekName } from "../api/models/Business";

// Mirrors Models.BusinessHoursStatus. `now` defaults to the browser's own local Date — that
// already reflects the viewer's time zone (see TimeZoneContext's own comment), so no explicit
// conversion is needed the way the server-rendered Blazor version required.
const DAY_NAMES: DayOfWeekName[] = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

function toMinutes(timeOnly: string): number {
  const [hours, minutes] = timeOnly.split(":").map(Number);
  return hours * 60 + minutes;
}

export function activeClosure(closures: BusinessClosureDto[], now: Date = new Date()): BusinessClosureDto | null {
  const today = toDateOnlyString(now);
  return closures.find((c) => today >= c.startDate && today <= c.endDate) ?? null;
}

// Null means hours haven't been configured yet — callers should treat that as "unknown", not "closed".
export function isOpenNow(hours: BusinessHoursDto[], closures: BusinessClosureDto[], now: Date = new Date()): boolean | null {
  if (activeClosure(closures, now)) return false;
  if (hours.length === 0) return null;

  const today = hours.find((h) => h.dayOfWeek === DAY_NAMES[now.getDay()]);
  if (!today || today.isClosed || !today.openTime || !today.closeTime) return false;

  const nowMinutes = now.getHours() * 60 + now.getMinutes();
  const openMinutes = toMinutes(today.openTime);
  const closeMinutes = toMinutes(today.closeTime);

  // Overnight windows (e.g. 22:00–02:00) close after midnight, so a simple open <= now < close
  // comparison would wrongly report "closed" for the whole stretch after midnight.
  return closeMinutes > openMinutes
    ? nowMinutes >= openMinutes && nowMinutes < closeMinutes
    : nowMinutes >= openMinutes || nowMinutes < closeMinutes;
}

export function todayDayName(now: Date = new Date()): DayOfWeekName {
  return DAY_NAMES[now.getDay()];
}

function toDateOnlyString(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export function formatHoursRow(row: BusinessHoursDto | undefined): string {
  if (!row || row.isClosed || !row.openTime || !row.closeTime) return "Closed";
  return `${formatTime(row.openTime)}–${formatTime(row.closeTime)}`;
}

function formatTime(timeOnly: string): string {
  const [hours, minutes] = timeOnly.split(":");
  return `${hours.padStart(2, "0")}:${minutes.padStart(2, "0")}`;
}

export const WEEK_ORDER: DayOfWeekName[] = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];
