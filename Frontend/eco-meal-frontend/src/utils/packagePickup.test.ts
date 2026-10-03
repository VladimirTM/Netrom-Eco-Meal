import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { closingSoonLabel, formatPickupWindow } from "./packagePickup";

describe("formatPickupWindow", () => {
  const pickupStart = "2026-03-15T18:30:00Z";
  const pickupEnd = "2026-03-15T20:00:00Z";

  it("renders the same UTC instant in different browser time zones", () => {
    expect(formatPickupWindow(pickupStart, pickupEnd, "Europe/Bucharest")).toBe("Mar 15 · 20:30–22:00");
    expect(formatPickupWindow(pickupStart, pickupEnd, "America/New_York")).toBe("Mar 15 · 14:30–16:00");
  });
});

describe("closingSoonLabel", () => {
  const now = new Date("2026-03-15T18:00:00Z");

  afterEach(() => {
    vi.useRealTimers();
  });

  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(now);
  });

  it("returns null when the window already closed", () => {
    expect(closingSoonLabel("2026-03-15T17:59:00Z", now)).toBeNull();
  });

  it("returns null when more than an hour remains", () => {
    expect(closingSoonLabel("2026-03-15T19:01:00Z", now)).toBeNull();
  });

  it("returns a minutes-remaining label within the one-hour window", () => {
    expect(closingSoonLabel("2026-03-15T18:30:00Z", now)).toBe("Ends in 30 min");
  });

  it("rounds up to at least 1 minute remaining", () => {
    expect(closingSoonLabel("2026-03-15T18:00:10Z", now)).toBe("Ends in 1 min");
  });
});
