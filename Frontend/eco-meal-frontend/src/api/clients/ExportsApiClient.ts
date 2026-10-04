import { http } from "../base/http";

function buildQuery(from: string | null, to: string | null, businessId: string | null): string {
  const query = new URLSearchParams();
  if (from) query.set("from", from);
  if (to) query.set("to", to);
  if (businessId) query.set("businessId", businessId);
  const qs = query.toString();
  return qs ? `?${qs}` : "";
}

// Ports ExportsController's two CSV routes. The frontend downloads via Axios (responseType:
// 'blob') and triggers the save itself — a JWT bearer can't ride along on a plain <a href>
// navigation the way Blazor's auth cookie did.
export const exportsApi = {
  exportOrdersCsv: (from: string | null, to: string | null, businessId: string | null): Promise<Blob> =>
    http.getBlob(`/orders/export${buildQuery(from, to, businessId)}`),

  exportPaymentsCsv: (from: string | null, to: string | null, businessId: string | null): Promise<Blob> =>
    http.getBlob(`/payments/export${buildQuery(from, to, businessId)}`),
};

// Saves a downloaded Blob under the given filename, the same way a browser-navigated download would.
export function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
