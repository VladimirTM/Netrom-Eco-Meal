// Pure helpers backing the Dashboard page's trend chart and business-analytics cards — split out
// of the component so the bucketing/aggregation math (ported from Dashboard.razor's @code block)
// stays testable without rendering anything.
import type { OrderDto } from "../api/models/Order";
import type { PackageDto } from "../api/models/Package";

export interface DailyStat {
  // "yyyy-MM-dd", the UTC calendar date — matches Dashboard.razor's own comment: "Bucketed in UTC,
  // not local time — good enough for a trend shape, no JS interop needed." DTOs already carry UTC
  // ISO 8601 ("Z") timestamps, so slicing the string is enough; no Date-object/timezone math needed.
  date: string;
  orderCount: number;
  kgSaved: number;
}

const TREND_DAYS = 14;
const CHART_HEIGHT_PX = 64;

// Midnight UTC, 13 days before `reference` — the start of the "last 14 days" window the trend
// chart and both analytics cards all share.
export function fourteenDaysAgoUtc(reference: Date = new Date()): Date {
  const start = new Date(Date.UTC(reference.getUTCFullYear(), reference.getUTCMonth(), reference.getUTCDate()));
  start.setUTCDate(start.getUTCDate() - (TREND_DAYS - 1));
  return start;
}

// Ports LoadDailyStatsAsync. `since` must be a UTC midnight (see fourteenDaysAgoUtc) and
// `rangeOrders` the result of ordersApi.getInRange(since, null, businessId) — i.e. already scoped
// to orders created on/after `since`.
export function buildDailyStats(rangeOrders: OrderDto[], since: Date): DailyStat[] {
  const stats: DailyStat[] = [];

  for (let i = 0; i < TREND_DAYS; i++) {
    const day = new Date(since);
    day.setUTCDate(day.getUTCDate() + i);
    const dayKey = day.toISOString().slice(0, 10);

    const ordersThatDay = rangeOrders.filter((o) => o.createdAt.slice(0, 10) === dayKey);
    const kgSaved = ordersThatDay
      .filter((o) => o.status === "Completed")
      .reduce((sum, o) => sum + o.lines.reduce((lineSum, l) => lineSum + l.quantity * l.weightKg, 0), 0);

    stats.push({ date: dayKey, orderCount: ordersThatDay.length, kgSaved });
  }

  return stats;
}

// Groups every Completed order's lines by packageId — the HTTP PackageDto (unlike the Blazor
// page's in-process `Package` entity) doesn't carry its own OrderPackages navigation, so this
// rebuilds the same "completed quantity per package" lookup from the orders side instead.
export function buildCompletedQtyByPackage(orders: OrderDto[]): Map<string, number> {
  const byPackage = new Map<string, number>();

  for (const order of orders) {
    if (order.status !== "Completed") continue;
    for (const line of order.lines) {
      byPackage.set(line.packageId, (byPackage.get(line.packageId) ?? 0) + line.quantity);
    }
  }

  return byPackage;
}

export interface AnalyticsStats {
  closedPackageCount: number;
  soldQty: number;
  unsoldQty: number;
  sellThroughRate: number;
  hourlyPickupQty: number[];
  maxHourlyQty: number;
}

// Ports LoadAnalyticsAsync + RecomputeHourlyPickupStats. `analyticsPackages` is PackagesApi's
// getForAnalytics(businessId, since) result (PickupStart >= since); `completedQtyByPackage` comes
// from buildCompletedQtyByPackage over the *full*, unfiltered order history for the business
// (ordersApi.getForManagement) — not date-limited, matching the Blazor page's own OrderPackages
// join, which isn't limited to orders created in the last 14 days either.
export function computeAnalyticsStats(analyticsPackages: PackageDto[], completedQtyByPackage: Map<string, number>, timeZone: string, now: Date = new Date()): AnalyticsStats {
  // Only counts closed pickup windows — an open package's remaining stock isn't "unsold" yet, it
  // just hasn't finished selling (Confirmed already decremented it like a sale).
  const closed = analyticsPackages.filter((p) => new Date(p.pickupEnd).getTime() < now.getTime());
  const soldQty = closed.reduce((sum, p) => sum + (completedQtyByPackage.get(p.id) ?? 0), 0);
  const unsoldQty = closed.reduce((sum, p) => sum + p.quantity, 0);
  const totalOffered = soldQty + unsoldQty;
  const sellThroughRate = totalOffered > 0 ? soldQty / totalOffered : 0;

  const hourlyPickupQty = new Array(24).fill(0) as number[];
  for (const p of analyticsPackages) {
    const completedQty = completedQtyByPackage.get(p.id) ?? 0;
    if (completedQty <= 0) continue;

    const hour = localHourInTimeZone(p.pickupStart, timeZone);
    hourlyPickupQty[hour] += completedQty;
  }

  return {
    closedPackageCount: closed.length,
    soldQty,
    unsoldQty,
    sellThroughRate,
    hourlyPickupQty,
    maxHourlyQty: Math.max(1, ...hourlyPickupQty),
  };
}

// Hour (0-23) that a UTC instant falls on in `timeZone` — the browser-side equivalent of
// ClientTimeZoneService.ToLocal(...).Hour. hourCycle "h23" avoids the "24" midnight quirk some
// engines produce for hour12: false.
export function localHourInTimeZone(isoUtc: string, timeZone: string): number {
  const parts = new Intl.DateTimeFormat("en-US", { timeZone, hour: "2-digit", hourCycle: "h23" }).formatToParts(new Date(isoUtc));
  const hourPart = parts.find((p) => p.type === "hour");
  return hourPart ? Number(hourPart.value) % 24 : new Date(isoUtc).getUTCHours();
}

// Pixels, not percentage height — same reasoning as Dashboard.razor's own comment: percentages
// don't reliably resolve through an unstretched flex-item chain.
export function barHeightPx(value: number, max: number): number {
  if (value <= 0) return 3;
  return Math.round(Math.min(CHART_HEIGHT_PX, Math.max(4, (value / max) * CHART_HEIGHT_PX)));
}

export function hourLabel(hour: number): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${pad(hour)}:00–${pad((hour + 1) % 24)}:00`;
}

// An int, not a float — avoids e.g. "70,0%" landing in inline CSS and silently breaking the width
// in the ro-RO locale (same reasoning as the Blazor page's own SellThroughRatePercent).
export function sellThroughRatePercent(rate: number): number {
  return Math.round(rate * 100);
}
