// DTOs carry UTC ISO 8601 ("Z") timestamps — these
// convert to the viewer's own local time for display only, the same split the server used to do.

export function formatLocalDateTime(isoUtc: string, timeZone: string): string {
  return new Intl.DateTimeFormat("ro-RO", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone,
  }).format(new Date(isoUtc));
}

export function formatLocalDate(isoUtc: string, timeZone: string): string {
  return new Intl.DateTimeFormat("ro-RO", {
    dateStyle: "medium",
    timeZone,
  }).format(new Date(isoUtc));
}

export function formatLocalTime(isoUtc: string, timeZone: string): string {
  return new Intl.DateTimeFormat("ro-RO", {
    timeStyle: "short",
    timeZone,
  }).format(new Date(isoUtc));
}
