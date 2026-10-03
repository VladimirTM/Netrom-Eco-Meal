import { describe, expect, it } from "vitest";
import { planRoute } from "./tripRoute";

describe("planRoute", () => {
  it("returns empty for no stops", () => {
    expect(planRoute([], null, null)).toEqual([]);
  });

  it("returns the single stop for one stop", () => {
    expect(planRoute([{ id: "only", lat: 45.75, lng: 21.22 }], null, null)).toEqual(["only"]);
  });

  it("starts from the first stop and walks nearest-neighbor when no start location is given", () => {
    // Roughly a west-to-east line: A(0,0) --far-- C(0,10) --near-- B(0,10.1)
    const a = { id: "a", lat: 0, lng: 0 };
    const b = { id: "b", lat: 0, lng: 10.1 };
    const c = { id: "c", lat: 0, lng: 10 };

    expect(planRoute([a, b, c], null, null)).toEqual(["a", "c", "b"]);
  });

  it("starts from the stop nearest the given location", () => {
    const far = { id: "far", lat: 0, lng: 20 };
    const near = { id: "near", lat: 0, lng: 1 };

    expect(planRoute([far, near], 0, 0.9)).toEqual(["near", "far"]);
  });
});
