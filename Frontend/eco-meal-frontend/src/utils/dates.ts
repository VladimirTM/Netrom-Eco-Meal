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

// A DateOnly ("yyyy-MM-dd", no time component — e.g. BusinessClosureDto's StartDate/EndDate) has
// no UTC instant to convert, unlike the DateTime fields above — parsing it with a time zone would
// risk shifting it a day in either direction for viewers behind/ahead of UTC.
export function formatDateOnly(dateOnly: string): string {
  const [year, month, day] = dateOnly.split("-").map(Number);
  return new Intl.DateTimeFormat("en-US", { month: "short", day: "numeric", year: "numeric" }).format(new Date(year, month - 1, day));
}
