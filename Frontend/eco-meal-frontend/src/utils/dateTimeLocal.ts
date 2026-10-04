// An <input type="datetime-local"> always works in the browser's own local time — the same zone
// TimeZoneContext detects via Intl — so, unlike dates.ts's display-only helpers, these don't take a
// timeZone parameter. Mirrors ClientTimeZoneService.ToLocal/ToUtc's role in the Blazor app, for the
// one spot (PackageForm) that round-trips a UTC instant through an editable local datetime field.

// UTC ISO 8601 instant -> the input's "YYYY-MM-DDTHH:mm" value, in the viewer's local time.
export function toDateTimeLocalInputValue(isoUtc: string): string {
  const d = new Date(isoUtc);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

// The input's local value -> a UTC ISO 8601 instant. Relies on the JS Date spec itself: a
// date-time string with no offset (e.g. "2024-01-01T10:00") parses as local time, so this is
// already the matching UTC instant once stringified.
export function fromDateTimeLocalInputValue(value: string): string {
  return new Date(value).toISOString();
}
