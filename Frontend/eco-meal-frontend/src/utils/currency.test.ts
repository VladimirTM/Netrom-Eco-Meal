import { describe, expect, it } from "vitest";
import { formatCurrency } from "./currency";

// Intl's ro-RO currency output separates the amount from "RON" with a non-breaking space
// (U+00A0), not a regular one — easy to miss since both render identically.
const NBSP = " ";

describe("formatCurrency", () => {
  it("formats using ro-RO grouping/decimal conventions with the RON symbol", () => {
    expect(formatCurrency(12.5)).toBe(`12,50${NBSP}RON`);
  });

  it("always shows two decimal places, even for whole numbers", () => {
    expect(formatCurrency(5)).toBe(`5,00${NBSP}RON`);
  });

  it("groups thousands with a dot", () => {
    expect(formatCurrency(1234.5)).toBe(`1.234,50${NBSP}RON`);
  });
});
