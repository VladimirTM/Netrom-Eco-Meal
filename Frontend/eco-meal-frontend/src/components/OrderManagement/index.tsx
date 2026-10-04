import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../../api/base/http";
import { businessesApi } from "../../api/clients/BusinessesApiClient";
import { downloadBlob, exportsApi } from "../../api/clients/ExportsApiClient";
import { ordersApi } from "../../api/clients/OrdersApiClient";
import type { BusinessDto } from "../../api/models/Business";
import type { OrderDto, OrderStatusName } from "../../api/models/Order";
import type { PaginatedList } from "../../api/models/Pagination";
import { useAuth } from "../../context/AuthContext/auth-context";
import { useManagedBusiness } from "../../context/ManagedBusinessContext/managed-business-context";
import ConfirmDialog from "../common/ConfirmDialog";
import ForbiddenPanel from "../common/ForbiddenPanel";
import OrderDetailModal from "../common/OrderDetailModal";
import Pagination from "../common/Pagination";
import { formatCurrency } from "../../utils/currency";
import { formatOrderPickupWindow } from "../../utils/packagePickup";
import { orderStatusCssSuffix, orderStatusLabel } from "../../utils/orderStatus";
import { paymentStatusBadgeClass, paymentStatusLabel } from "../../utils/paymentStatus";
import { useTimeZone } from "../../context/TimeZoneContext/timezone-context";

const STATUS_ORDER: OrderStatusName[] = ["Pending", "Confirmed", "Completed", "NoShow", "Cancelled"];
const PAGE_SIZE = 10;

// Allowed transitions mirror OrderService.UpdateStatusAsync — keep in sync if that ever changes.
function pickupWindowPassed(order: OrderDto): boolean {
  return order.lines.length > 0 && order.lines.every((l) => new Date(l.pickupEnd).getTime() < Date.now());
}

// Ports OrderManagement.razor (/orders/manage). Admin sees every business; a BusinessManager is
// scoped to whichever business is currently selected in the sidebar switcher.
function OrderManagement() {
  const { user } = useAuth();
  const timeZone = useTimeZone();
  const isAdmin = user?.role === "Admin";
  const { selectedBusinessId } = useManagedBusiness();

  const [paged, setPaged] = useState<PaginatedList<OrderDto> | null>(null);
  const [allBusinesses, setAllBusinesses] = useState<BusinessDto[]>([]);
  const [search, setSearch] = useState("");
  const [businessFilter, setBusinessFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [pageIndex, setPageIndex] = useState(1);
  const [forbidden, setForbidden] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busyOrderId, setBusyOrderId] = useState<string | null>(null);
  const [selectedOrder, setSelectedOrder] = useState<OrderDto | null>(null);
  const [pendingCancel, setPendingCancel] = useState<OrderDto | null>(null);
  const [cancelling, setCancelling] = useState(false);
  const [exportOpen, setExportOpen] = useState(false);
  const [exportFrom, setExportFrom] = useState("");
  const [exportTo, setExportTo] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [resetForBusinessId, setResetForBusinessId] = useState(selectedBusinessId);

  const effectiveBusinessId = isAdmin ? businessFilter || undefined : (selectedBusinessId ?? undefined);

  // Switching businesses in the sidebar resets back to page 1 rather than risking a now-out-of-range
  // page for the new business — derived at render time rather than a useEffect round trip (same
  // pattern CartProvider uses for its own per-user reload).
  if (selectedBusinessId !== resetForBusinessId) {
    setResetForBusinessId(selectedBusinessId);
    setPageIndex(1);
  }

  // Mirrors the Blazor page's own Debouncer — search reloads 300ms after the last keystroke, every
  // other filter reloads immediately (see onBusinessFilterChange/onStatusFilterChange/clearFilters).
  useEffect(() => {
    const handle = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(handle);
  }, [search]);

  const loadPage = useCallback(async () => {
    try {
      const page = await ordersApi.getForManagementPaged(pageIndex, PAGE_SIZE, debouncedSearch || null, effectiveBusinessId ?? null, statusFilter || null);
      setPaged(page);
      setForbidden(false);
    } catch (err) {
      if (err instanceof ApiError && (err.status === 401 || err.status === 403)) {
        setForbidden(true);
      }
    }
  }, [pageIndex, debouncedSearch, effectiveBusinessId, statusFilter]);

  useEffect(() => {
    if (isAdmin) businessesApi.getAll(false).then((list) => setAllBusinesses([...list].sort((a, b) => a.name.localeCompare(b.name))));
  }, [isAdmin]);

  useEffect(() => {
    // Synchronizes with the server whenever paging, search or any filter changes.
    // oxlint-disable-next-line react/set-state-in-effect
    loadPage();
  }, [loadPage]);

  function onSearchInput(value: string) {
    setSearch(value);
    setPageIndex(1);
  }

  function onBusinessFilterChange(value: string) {
    setBusinessFilter(value);
    setPageIndex(1);
  }

  function onStatusFilterChange(value: string) {
    setStatusFilter(value);
    setPageIndex(1);
  }

  function clearFilters() {
    setSearch("");
    setBusinessFilter("");
    setStatusFilter("");
    setPageIndex(1);
  }

  async function changeStatus(order: OrderDto, statusName: OrderStatusName) {
    setActionError(null);
    setBusyOrderId(order.id);
    try {
      const updated = await ordersApi.updateStatus(order.id, statusName);
      setPaged((prev) => (prev ? { ...prev, items: prev.items.map((o) => (o.id === updated.id ? updated : o)) } : prev));
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : "That status change couldn't be completed.");
    } finally {
      setBusyOrderId(null);
    }
  }

  async function confirmCancel() {
    if (!pendingCancel) return;
    setCancelling(true);
    await changeStatus(pendingCancel, "Cancelled");
    setCancelling(false);
    setPendingCancel(null);
  }

  async function downloadCsv() {
    const from = exportFrom || null;
    // Inclusive of the whole "to" day, not just its midnight.
    const to = exportTo ? new Date(new Date(exportTo).getTime() + 86400000).toISOString().slice(0, 10) : null;
    const blob = await exportsApi.exportOrdersCsv(from, to, effectiveBusinessId ?? null);
    downloadBlob(blob, `orders-${new Date().toISOString().slice(0, 10)}.csv`);
    setExportOpen(false);
  }

  if (forbidden) {
    return <ForbiddenPanel message="You don't manage a business yet, so there are no orders to show." backHref="/dashboard" backLabel="Back to dashboard" />;
  }

  return (
    <div>
      <div className="d-flex justify-content-between align-items-start mb-4">
        <div>
          <h1 className="h3 fw-bold mb-1">Manage Orders</h1>
          <p className="text-muted small mb-0">{isAdmin ? "Confirm, complete, or cancel orders across every business" : "Confirm, complete, or cancel orders placed at your business"}</p>
        </div>
        {!isAdmin && (
          <Link to="/orders/scan" className="btn btn-primary px-4">
            <i className="bi bi-qr-code-scan me-1" /> Scan pickup
          </Link>
        )}
      </div>

      {actionError && (
        <div className="alert alert-danger" role="alert">
          {actionError}
        </div>
      )}

      {paged && (
        <div className="d-flex gap-3 mb-3 flex-wrap">
          <div className="input-group" style={{ maxWidth: 320 }}>
            <span className="input-group-text border-end-0">
              <i className="bi bi-search text-muted" />
            </span>
            <input
              type="text"
              className="form-control border-start-0 ps-0"
              placeholder="Search order # or customer…"
              value={search}
              onChange={(e) => onSearchInput(e.target.value)}
            />
          </div>
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
          <select className="form-select" style={{ maxWidth: 180 }} value={statusFilter} onChange={(e) => onStatusFilterChange(e.target.value)}>
            <option value="">All statuses</option>
            {STATUS_ORDER.map((s) => (
              <option value={s} key={s}>
                {orderStatusLabel(s)}
              </option>
            ))}
          </select>
          {(search !== "" || businessFilter !== "" || statusFilter !== "") && (
            <button className="btn btn-outline-secondary" onClick={clearFilters}>
              <i className="bi bi-x-lg me-1" /> Clear
            </button>
          )}
          <button type="button" className="btn btn-outline-secondary" onClick={() => setExportOpen(true)}>
            <i className="bi bi-download me-1" /> Export CSV
          </button>
          <span className="align-self-center text-muted small ms-auto">
            {paged.totalCount} result{paged.totalCount === 1 ? "" : "s"}
          </span>
        </div>
      )}

      {exportOpen && (
        <>
          <div className="em-popover-backdrop" onClick={() => setExportOpen(false)} />
          <div className="em-popover orders-export-panel">
            <div className="em-popover-header">
              <span>Export CSV</span>
              <button type="button" className="em-popover-close" aria-label="Close export" onClick={() => setExportOpen(false)}>
                <i className="bi bi-x-lg" />
              </button>
            </div>
            <div className="em-popover-body">
              <div className="em-popover-field">
                <label className="form-label small text-muted mb-1">From</label>
                <input type="date" className="form-control" value={exportFrom} onChange={(e) => setExportFrom(e.target.value)} />
              </div>
              <div className="em-popover-field">
                <label className="form-label small text-muted mb-1">To</label>
                <input type="date" className="form-control" value={exportTo} onChange={(e) => setExportTo(e.target.value)} />
              </div>
              {isAdmin && businessFilter !== "" && <p className="text-muted small mb-0">Scoped to the business selected in the filters above.</p>}
            </div>
            <div className="em-popover-footer">
              <button type="button" className="btn btn-primary btn-sm ms-auto" onClick={downloadCsv}>
                <i className="bi bi-download me-1" /> Download CSV
              </button>
            </div>
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
              <i className="bi bi-receipt" />
            </div>
            <p className="text-muted mb-0">{paged.totalCount === 0 && search === "" && businessFilter === "" && statusFilter === "" ? "No orders have been placed yet." : "No orders match your filters."}</p>
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
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Items</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Total</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Pickup</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Status</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Payment</th>
                    <th className="py-3 text-uppercase text-muted small fw-semibold">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {paged.items.map((order) => (
                    <tr className="order-row-clickable" key={order.id} onClick={() => setSelectedOrder(order)}>
                      <td className="px-4 fw-semibold">
                        #{String(order.orderNumber).padStart(3, "0")}
                        {order.logisticsNote && <i className="bi bi-chat-left-text text-primary ms-1" title={order.logisticsNote} />}
                      </td>
                      {isAdmin && <td>{order.businessName}</td>}
                      <td>{order.customerName}</td>
                      <td>
                        {order.lines.map((line) => (
                          <div className="small text-nowrap" key={line.packageId}>
                            {line.quantity}&times; {line.packageName}
                          </div>
                        ))}
                      </td>
                      <td className="fw-semibold">{formatCurrency(order.lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0))}</td>
                      <td className="text-muted small">{formatOrderPickupWindow(order.lines, timeZone)}</td>
                      <td>
                        <span className={`order-status-badge order-status-${orderStatusCssSuffix(order.status)}`}>{orderStatusLabel(order.status)}</span>
                        {order.status === "Confirmed" && order.pickupPasses.length > 1 && (
                          <span className="order-multi-pass-hint" title={`Split across ${order.pickupPasses.length} pickup passes`}>
                            <i className="bi bi-people" /> {order.pickupPasses.length}
                          </span>
                        )}
                      </td>
                      <td>
                        {order.payment && <span className={`badge ${paymentStatusBadgeClass(order.payment.status)}`}>{paymentStatusLabel(order.payment.status)}</span>}
                      </td>
                      <td onClick={(e) => e.stopPropagation()}>
                        {busyOrderId === order.id ? (
                          <div className="spinner-border spinner-border-sm text-muted" role="status">
                            <span className="visually-hidden">Updating...</span>
                          </div>
                        ) : order.status === "Pending" ? (
                          <div className="order-action-group">
                            <button type="button" className="order-action-btn order-action-confirm" onClick={() => changeStatus(order, "Confirmed")}>
                              <i className="bi bi-check-lg" /> Confirm
                            </button>
                            <button type="button" className="order-action-btn order-action-cancel" onClick={() => setPendingCancel(order)}>
                              Cancel
                            </button>
                          </div>
                        ) : order.status === "Confirmed" ? (
                          <div className="order-action-group">
                            <button type="button" className="order-action-btn order-action-complete" onClick={() => changeStatus(order, "Completed")}>
                              <i className="bi bi-check2-all" /> Complete
                            </button>
                            <button
                              type="button"
                              className="order-action-btn order-action-noshow"
                              disabled={!pickupWindowPassed(order)}
                              title={pickupWindowPassed(order) ? undefined : "You can't mark a no-show before the pickup window closes."}
                              onClick={() => changeStatus(order, "NoShow")}
                            >
                              No-show
                            </button>
                            <button type="button" className="order-action-btn order-action-cancel" onClick={() => setPendingCancel(order)}>
                              Cancel
                            </button>
                          </div>
                        ) : (
                          <span className="order-action-empty">—</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination currentPage={paged.pageIndex} totalPages={paged.totalPages} onPageChange={setPageIndex} />
          </>
        )}
      </div>

      <OrderDetailModal order={selectedOrder} pickupLabel={selectedOrder ? formatOrderPickupWindow(selectedOrder.lines, timeZone) : ""} onClose={() => setSelectedOrder(null)} />

      <ConfirmDialog
        isOpen={pendingCancel !== null}
        title="Cancel order?"
        message={pendingCancel ? `Order #${String(pendingCancel.orderNumber).padStart(3, "0")} will be cancelled, refunding the customer if it was paid. This can't be undone.` : ""}
        confirmLabel="Cancel order"
        busy={cancelling}
        onConfirm={confirmCancel}
        onCancel={() => setPendingCancel(null)}
      />
    </div>
  );
}

export default OrderManagement;
