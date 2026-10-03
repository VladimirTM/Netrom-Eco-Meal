import { describe, expect, it } from "vitest";
import { availableQuantity } from "./packageAvailability";

describe("availableQuantity", () => {
  it("subtracts reservations and basket quantity from stock", () => {
    expect(availableQuantity(10, 3, 2)).toBe(5);
  });

  it("defaults the basket quantity to 0", () => {
    expect(availableQuantity(10, 4)).toBe(6);
  });

  it("never goes negative when reservations exceed stock", () => {
    expect(availableQuantity(5, 8, 0)).toBe(0);
  });

  it("never goes negative when basket quantity exceeds remaining stock", () => {
    expect(availableQuantity(5, 3, 10)).toBe(0);
  });

  it("returns the full stock when nothing is reserved or basketed", () => {
    expect(availableQuantity(7, 0, 0)).toBe(7);
  });
});
