// Mirrors PaymentStatuses (Backend/NetromEcoMeal.DataAccess/Constants/PaymentStatuses.cs).
export type PaymentStatusName = "Succeeded" | "Refunded" | "RefundFailed";

export function paymentStatusLabel(status: string): string {
  switch (status) {
    case "Refunded":
      return "Refunded";
    case "RefundFailed":
      return "Refund failed";
    default:
      return "Paid";
  }
}

export function paymentStatusBadgeClass(status: string): string {
  switch (status) {
    case "Refunded":
      return "bg-secondary-subtle text-secondary";
    case "RefundFailed":
      return "bg-danger-subtle text-danger";
    default:
      return "bg-success-subtle text-success";
  }
}

export function paymentStatusIconClass(status: string): string {
  switch (status) {
    case "Refunded":
      return "bi-arrow-counterclockwise";
    case "RefundFailed":
      return "bi-exclamation-triangle";
    default:
      return "bi-check-circle";
  }
}
