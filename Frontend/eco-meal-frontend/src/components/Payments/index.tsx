import { useCallback, useEffect, useState } from "react";
import { ApiError } from "../../api/base/http";
import { businessesApi } from "../../api/clients/BusinessesApiClient";
import { downloadBlob, exportsApi } from "../../api/clients/ExportsApiClient";
import { ordersApi } from "../../api/clients/OrdersApiClient";
import type { BusinessDto } from "../../api/models/Business";
import type { OrderDto } from "../../api/models/Order";
import type { PaginatedList } from "../../api/models/Pagination";
import { useAuth } from "../../context/AuthContext/auth-context";
import { useManagedBusiness } from "../../context/ManagedBusinessContext/managed-business-context";
import { useTimeZone } from "../../context/TimeZoneContext/timezone-context";
import ForbiddenPanel from "../common/ForbiddenPanel";
import Pagination from "../common/Pagination";
import { formatCurrency } from "../../utils/currency";
import { formatLocalDateTime } from "../../utils/dates";
import { paymentStatusBadgeClass, paymentStatusLabel } from "../../utils/paymentStatus";

const PAGE_SIZE = 15;

// Ports Payments.razor (/payments) — a read-only ledger. Admin sees every business; a
// BusinessManager is scoped to whichever business is currently selected in the sidebar switcher.
function Payments() {
  const { user } = useAuth();
  const timeZone = useTimeZone();
  const isAdmin = user?.role === "Admin";
  const { selectedBusinessId } = useManagedBusiness();

  const [paged, setPaged] = useState<PaginatedList<OrderDto> | null>(null);
  const [allBusinesses, setAllBusinesses] = useState<BusinessDto[]>([]);
  const [businessFilter, setBusinessFilter] = useState("");
  const [pageIndex, setPageIndex] = useState(1);
  const [forbidden, setForbidden] = useState(false);
  const [exportFrom, setExportFrom] = useState("");
  const [exportTo, setExportTo] = useState("");
  const [resetForBusinessId, setResetForBusinessId] = useState(selectedBusinessId);

  const effectiveBusinessId = isAdmin ? businessFilter || undefined : (selectedBusinessId ?? undefined);

  // Switching businesses in the sidebar resets back to page 1 rather than risking a now-out-of-range
  // page for the new business — derived at render time rather than a useEffect round trip (same
  // pattern CartProvider uses for its own per-user reload).
  if (selectedBusinessId !== resetForBusinessId) {
    setResetForBusinessId(selectedBusinessId);
    setPageIndex(1);
  }

  const loadPage = useCallback(async () => {
    try {
      const page = await ordersApi.getForManagementPaged(pageIndex, PAGE_SIZE, null, effectiveBusinessId ?? null, null);
      setPaged(page);
      setForbidden(false);
    } catch (err) {
      if (err instanceof ApiError && (err.status === 401 || err.status === 403)) {
        setForbidden(true);
      }
    }
  }, [pageIndex, effectiveBusinessId]);

  useEffect(() => {
    if (isAdmin) businessesApi.getAll(false).then((list) => setAllBusinesses([...list].sort((a, b) => a.name.localeCompare(b.name))));
  }, [isAdmin]);

  useEffect(() => {
    // Synchronizes with the server whenever paging or the business filter changes.
    // oxlint-disable-next-line react/set-state-in-effect
    loadPage();
  }, [loadPage]);

  function onBusinessFilterChange(value: string) {
    setBusinessFilter(value);
    setPageIndex(1);
  }

  async function downloadCsv() {
    const from = exportFrom || null;
    const to = exportTo ? new Date(new Date(exportTo).getTime() + 86400000).toISOString().slice(0, 10) : null;
    const blob = await exportsApi.exportPaymentsCsv(from, to, effectiveBusinessId ?? null);
    downloadBlob(blob, `payments-${new Date().toISOString().slice(0, 10)}.csv`);
  }

  if (forbidden) {
    return <ForbiddenPanel message="You don't manage a business yet, so there are no payments to show." backHref="/dashboard" backLabel="Back to dashboard" />;
  }

  const paidOrders = paged?.items.filter((o) => o.payment !== null) ?? [];
  const collectedThisPage = paidOrders.reduce((sum, o) => sum + (o.payment?.amount ?? 0), 0);
  const refundedThisPage = paidOrders.filter((o) => o.payment?.status === "Refunded").reduce((sum, o) => sum + (o.payment?.amount ?? 0), 0);
  const refundFailedCountThisPage = paidOrders.filter((o) => o.payment?.status === "RefundFailed").length;

  return (
    <div>
      <div className="mb-4">
        <h1 className="h3 fw-bold mb-1">Payments</h1>
        <p className="text-muted small mb-0">{isAdmin ? "Every payment collected across all businesses" : "Payments collected at your business"}</p>
      </div>

      {paged && (
        <>
          <div className="row g-3 mb-4">
            <div className="col-sm-6 col-xl-3">
              <div className="card border-0 shadow-sm h-100 card-accent-green">
                <div className="card-body d-flex align-items-center gap-3">
                  <div className="rounded-3 p-3 bg-success bg-opacity-10 text-success">
                    <i className="bi bi-cash-stack fs-4" />
                  </div>
                  <div>
                    <div className="text-muted small">Collected (this page)</div>
                    <div className="fw-bold fs-5">{formatCurrency(collectedThisPage)}</div>
                  </div>
                </div>
              </div>
            </div>
            <div className="col-sm-6 col-xl-3">
              <div className="card border-0 shadow-sm h-100 card-accent-amber">
                <div className="card-body d-flex align-items-center gap-3">
                  <div className="rounded-3 p-3 bg-secondary bg-opacity-10 text-secondary">
                    <i className="bi bi-arrow-counterclockwise fs-4" />
                  </div>
                  <div>
                    <div className="text-muted small">Refunded (this page)</div>
                    <div className="fw-bold fs-5">{formatCurrency(refundedThisPage)}</div>
                  </div>
                </div>
              </div>
            </div>
            {refundFailedCountThisPage > 0 && (
              <div className="col-sm-6 col-xl-3">
                <div className="card border-0 shadow-sm h-100 card-accent-red">
                  <div className="card-body d-flex align-items-center gap-3">
                    <div className="rounded-3 p-3 bg-danger bg-opacity-10 text-danger">
                      <i className="bi bi-exclamation-triangle fs-4" />
                    </div>
                    <div>
                      <div className="text-muted small">Refund failed (this page)</div>
                      <div className="fw-bold fs-5">
                        {refundFailedCountThisPage} order{refundFailedCountThisPage === 1 ? "" : "s"}
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>

          <div className="d-flex gap-3 mb-3 flex-wrap">
            {isAdmin && (
              <select className="form-select" style={{ maxWidth: 220 }} value={businessFilter} onChange={(e) => onBusinessFilterChange(e.target.value)}>
                <option value="">All businesses</option>
                {allBusinesses.map((b) => (
                  <option value={b.id} key={b.id}>
                    {b.name}
                  </option>
                ))}
              </select>
            )}
            <span className="align-self-center text-muted small ms-auto">
              {paged.totalCount} order{paged.totalCount === 1 ? "" : "s"}
            </span>
          </div>

          <div className="d-flex align-items-end gap-2 mb-3 flex-wrap">
            <div>
              <label className="form-label small text-muted mb-1">Export from</label>
              <input type="date" className="form-control" value={exportFrom} onChange={(e) => setExportFrom(e.target.value)} />
            </div>
            <div>
              <label className="form-label small text-muted mb-1">Export to</label>
              <input type="date" className="form-control" value={exportTo} onChange={(e) => setExportTo(e.target.value)} />
            </div>
            <button type="button" className="btn btn-outline-secondary" onClick={downloadCsv}>
              <i className="bi bi-download me-1" /> Export CSV
            </button>
          </div>
        </>
      )}

      <div className="card border-0 shadow-sm">
        {paged === null ? (
          <div className="d-flex justify-content-center py-5">
            <div className="spinner-border text-primary" role="status">
              <span className="visually-hidden">Loading...</span>
            </div>
          </div>
        ) : paged.items.length === 0 ? (
          <div className="text-center py-5">
            <div className="em-empty-icon">
              <i className="bi bi-credit-card" />
            </div>
            <p className="text-muted mb-0">No orders have been placed yet.</p>
          </div>
        ) : (
          <>
            <div className="table-responsive">
              <table className="table table-hover mb-0 align-middle">
                <thead>
                  <tr className="border-bottom">
                    <th className="px-4 py-3 text-uppercase text-muted small fw-semibold">Order</th>
                    {isAdmin && <th className="py-3 text-uppercase text-muted small fw-semibold">Business</th>}
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Customer</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Amount</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Payment</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Paid</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Refunded</th>
                  </tr>
                </thead>
                <tbody>
                  {paged.items.map((order) => (
                    <tr key={order.id}>
                      <td className="px-4 fw-semibold">#{String(order.orderNumber).padStart(3, "0")}</td>
                      {isAdmin && <td>{order.businessName}</td>}
                      <td>{order.customerName}</td>
                      <td className="fw-semibold">{order.payment ? formatCurrency(order.payment.amount) : "—"}</td>
                      <td>
                        {order.payment ? (
                          <span className={`badge ${paymentStatusBadgeClass(order.payment.status)}`}>{paymentStatusLabel(order.payment.status)}</span>
                        ) : (
                          <span className="badge bg-warning-subtle text-warning">Unpaid</span>
                        )}
                      </td>
                      <td className="text-muted small">{order.payment ? formatLocalDateTime(order.payment.createdAt, timeZone) : "—"}</td>
                      <td className="text-muted small">{order.payment?.refundedAt ? formatLocalDateTime(order.payment.refundedAt, timeZone) : "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination currentPage={paged.pageIndex} totalPages={paged.totalPages} onPageChange={setPageIndex} />
          </>
        )}
      </div>
    </div>
  );
}

export default Payments;
