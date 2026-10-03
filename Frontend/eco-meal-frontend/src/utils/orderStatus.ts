import type { OrderStatusName } from "../api/models/Order";

// Mirrors OrderStatuses.Label (Backend/NetromEcoMeal.DataAccess/Constants/OrderStatuses.cs) — the
// stored value stays one PascalCase word (matches order-status-noshow's CSS class), but "NoShow"
// reads as one run-on word wherever it's shown to a customer.
export function orderStatusLabel(status: OrderStatusName): string {
  return status === "NoShow" ? "No-show" : status;
}

export function orderStatusCssSuffix(status: OrderStatusName): string {
  return status.toLowerCase();
}
