import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ordersApi } from "../../api/clients/OrdersApiClient";
import { streaksApi } from "../../api/clients/StreaksApiClient";
import type { OrderDto, OrderStatusName } from "../../api/models/Order";
import type { PaginatedList } from "../../api/models/Pagination";
import ConfirmDialog from "../common/ConfirmDialog";
import OrderDetailModal from "../common/OrderDetailModal";
import Pagination from "../common/Pagination";
import { useCart } from "../../context/CartContext/cart-context";
import { useTimeZone } from "../../context/TimeZoneContext/timezone-context";
import { formatCurrency } from "../../utils/currency";
import { formatOrderPickupWindow } from "../../utils/packagePickup";
import { orderStatusCssSuffix, orderStatusLabel } from "../../utils/orderStatus";
import { paymentStatusBadgeClass, paymentStatusIconClass, paymentStatusLabel } from "../../utils/paymentStatus";

const STATUS_ORDER: OrderStatusName[] = ["Pending", "Confirmed", "Completed", "NoShow", "Cancelled"];
const PAGE_SIZE = 10;

// Ports Orders.razor — hero lifetime stats (unfiltered), status filter chips driving a separate
// paginated query, ticket cards with status-gated actions (cancel/QR/reorder).
function Orders() {
  const cart = useCart();
  const timeZone = useTimeZone();

  const [orders, setOrders] = useState<OrderDto[] | null>(null);
  const [paged, setPaged] = useState<PaginatedList<OrderDto> | null>(null);
  const [streakWeeks, setStreakWeeks] = useState<number | null>(null);
  const [statusFilter, setStatusFilter] = useState<OrderStatusName | "">("");
  const [pageIndex, setPageIndex] = useState(1);

  const [pendingCancel, setPendingCancel] = useState<OrderDto | null>(null);
  const [cancelling, setCancelling] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [selectedOrder, setSelectedOrder] = useState<OrderDto | null>(null);

  const [pendingReorder, setPendingReorder] = useState<OrderDto | null>(null);
  const [reorderingOrderId, setReorderingOrderId] = useState<string | null>(null);
  const [reorderMessage, setReorderMessage] = useState<string | null>(null);
  const [reorderMessageIsError, setReorderMessageIsError] = useState(false);

  const loadPage = useCallback(async (status: OrderStatusName | "", page: number) => {
    setPaged(await ordersApi.getMinePaged(page, PAGE_SIZE, status === "" ? null : status));
  }, []);

  useEffect(() => {
    (async () => {
      setOrders(await ordersApi.getMine());
      await loadPage("", 1);
      setStreakWeeks(await streaksApi.getMyStreakWeeks());
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const completedOrders = orders?.filter((o) => o.status === "Completed") ?? [];
  const portionsRescued = completedOrders.reduce((sum, o) => sum + o.lines.reduce((s, l) => s + l.quantity, 0), 0);
  const kitchensVisited = new Set(completedOrders.map((o) => o.businessId)).size;
  const kgSaved = completedOrders.reduce((sum, o) => sum + o.lines.reduce((s, l) => s + l.quantity * l.weightKg, 0), 0);

  async function changeFilter(status: OrderStatusName | "") {
    setStatusFilter(status);
    setPageIndex(1);
    await loadPage(status, 1);
  }

  async function changePage(page: number) {
    setPageIndex(page);
    await loadPage(statusFilter, page);
  }

  async function confirmCancel() {
    if (!pendingCancel) return;
    setCancelling(true);
    setActionError(null);
    try {
      await ordersApi.cancel(pendingCancel.id);
      setOrders(await ordersApi.getMine());
      await loadPage(statusFilter, pageIndex);
    } catch (error) {
      setActionError(error instanceof Error ? error.message : "That order couldn't be cancelled.");
    } finally {
      setCancelling(false);
      setPendingCancel(null);
    }
  }

  async function addReorderLines(order: OrderDto) {
    setReorderingOrderId(order.id);
    let addedCount = 0;
    let skippedCount = 0;
    const now = Date.now();

    // Skips lines whose pickup window has already closed, same as Blazor's own reorder check.
    // Unlike Blazor (which had the full Package entity on hand), OrderDto carries no *live* stock
    // quantity, so a line whose package has since sold out isn't caught here — CartContext's own
    // addItem clamps to whatever quantity it's given, so the worst case is a line added at its
    // original quantity that the basket then has to reconcile once the customer opens it.
    for (const line of order.lines) {
      if (new Date(line.pickupEnd).getTime() <= now) {
        skippedCount++;
        continue;
      }
      cart.addItem(order.businessId, order.businessName, { id: line.packageId, name: line.packageName, price: line.unitPrice, quantity: line.quantity }, line.quantity);
      addedCount++;
    }

    setReorderMessageIsError(addedCount === 0);
    setReorderMessage(
      addedCount === 0
        ? "None of the items from this order are available right now."
        : skippedCount === 0
          ? `Added ${addedCount} item${addedCount === 1 ? "" : "s"} from ${order.businessName} to your basket.`
          : `Added ${addedCount} item${addedCount === 1 ? "" : "s"} from ${order.businessName} — ${skippedCount} item${skippedCount === 1 ? "" : "s"} no longer available.`,
    );
    setReorderingOrderId(null);
  }

  async function requestReorder(order: OrderDto) {
    setReorderMessage(null);
    if (cart.wouldReplaceCart(order.businessId)) {
      setPendingReorder(order);
      return;
    }
    await addReorderLines(order);
  }

  async function confirmReorder() {
    const order = pendingReorder;
    setPendingReorder(null);
    if (order) await addReorderLines(order);
  }

  return (
    <>
      <section className="orders-hero">
        <div className="orders-hero-inner">
          <span className="orders-hero-eyebrow">
            <i className="bi bi-receipt" /> Pickup history
          </span>
          <h1 className="orders-hero-title">Your orders</h1>
          <p className="orders-hero-sub">Every rescue you&apos;ve claimed. Show the ticket number to the kitchen at pickup.</p>

          <div className="orders-stats">
            <div className="orders-stat">
              <span className="orders-stat-value">{orders === null ? "—" : orders.length}</span>
              <span className="orders-stat-label">orders placed</span>
            </div>
            <div className="orders-stat">
              <span className="orders-stat-value">{orders === null ? "—" : portionsRescued}</span>
              <span className="orders-stat-label">portions rescued</span>
            </div>
            <div className="orders-stat">
              <span className="orders-stat-value">{orders === null ? "—" : kitchensVisited}</span>
              <span className="orders-stat-label">kitchens visited</span>
            </div>
            <div className="orders-stat">
              <span className="orders-stat-value">{orders === null ? "—" : kgSaved.toFixed(1).replace(/\.0$/, "")}</span>
              <span className="orders-stat-label">kg saved</span>
            </div>
            <div className="orders-stat">
              <span className="orders-stat-value">
                {streakWeeks === null ? "—" : streakWeeks}
                {streakWeeks !== null && streakWeeks > 0 && <i className="bi bi-fire text-warning ms-1" />}
              </span>
              <span className="orders-stat-label">week streak</span>
            </div>
          </div>
        </div>
      </section>

      <section className="orders-list-section">
        {actionError && (
          <div className="alert alert-danger" role="alert">
            {actionError}
          </div>
        )}
        {reorderMessage && (
          <div className={`alert ${reorderMessageIsError ? "alert-danger" : "alert-success"}`} role="alert">
            {reorderMessage}
          </div>
        )}

        {orders !== null && orders.length > 0 && (
          <div className="orders-chip-row">
            <button type="button" className={`orders-chip ${statusFilter === "" ? "orders-chip-active" : ""}`} onClick={() => void changeFilter("")}>
              All
            </button>
            {STATUS_ORDER.map((status) => (
              <button
                type="button"
                className={`orders-chip ${statusFilter === status ? "orders-chip-active" : ""}`}
                key={status}
                onClick={() => void changeFilter(status)}
              >
                {orderStatusLabel(status)}
              </button>
            ))}
          </div>
        )}

        {orders === null || paged === null ? (
          <div className="d-flex justify-content-center py-5">
            <div className="spinner-border text-primary" role="status">
              <span className="visually-hidden">Loading...</span>
            </div>
          </div>
        ) : orders.length === 0 ? (
          <div className="orders-empty">
            <div className="em-empty-icon">
              <i className="bi bi-basket2" />
            </div>
            <p className="mb-1">No orders yet.</p>
            <span className="text-muted small d-block mb-3">Rescued food from a kitchen near you shows up here.</span>
            <Link to="/" className="btn btn-primary px-4">
              Browse packages
            </Link>
          </div>
        ) : paged.items.length === 0 ? (
          <div className="orders-empty">
            <div className="em-empty-icon">
              <i className="bi bi-filter-circle" />
            </div>
            <p className="mb-0">No orders match this status.</p>
          </div>
        ) : (
          <>
            <div className="orders-stack">
              {paged.items.map((order) => {
                const subtotal = order.lines.reduce((s, l) => s + l.quantity * l.unitPrice, 0);
                const charged = order.payment?.amount ?? subtotal;
                return (
                  <article
                    className="order-ticket order-ticket-clickable"
                    role="button"
                    tabIndex={0}
                    key={order.id}
                    onClick={() => setSelectedOrder(order)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") setSelectedOrder(order);
                    }}
                  >
                    <div className="order-ticket-main">
                      <div className="order-ticket-top">
                        <div className="order-ticket-kitchen">
                          <div className="order-ticket-business">
                            <i className="bi bi-shop" />
                            {order.businessName}
                          </div>
                        </div>
                        <span className={`order-status-badge order-status-${orderStatusCssSuffix(order.status)}`}>{orderStatusLabel(order.status)}</span>
                      </div>

                      {order.payment && (
                        <span className={`badge ${paymentStatusBadgeClass(order.payment.status)} align-self-start mb-2`}>
                          <i className={`bi ${paymentStatusIconClass(order.payment.status)}`} /> {paymentStatusLabel(order.payment.status)}
                        </span>
                      )}

                      <div className="order-ticket-items">
                        {order.lines.map((line) => (
                          <div className="order-ticket-line" key={line.packageId}>
                            <span className="order-ticket-line-qty">{line.quantity}&times;</span>
                            <span className="order-ticket-line-name">{line.packageName}</span>
                            <span className="order-ticket-line-price">{formatCurrency(line.quantity * line.unitPrice)}</span>
                          </div>
                        ))}
                      </div>

                      <div className="order-ticket-footer">
                        <div className="order-ticket-pickup">
                          <i className="bi bi-clock" />
                          Pickup {formatOrderPickupWindow(order.lines, timeZone)}
                        </div>
                        <div className="order-ticket-total">
                          <span>Total</span>
                          <span className="order-ticket-total-value">
                            {formatCurrency(charged)}
                            {order.payment && charged < subtotal && <span className="text-muted small fw-normal"> ({formatCurrency(subtotal)} before discount)</span>}
                          </span>
                        </div>
                      </div>

                      {order.status === "Pending" || order.status === "Confirmed" ? (
                        <div className="order-ticket-actions">
                          {order.status === "Confirmed" && (
                            <Link to={`/orders/pickup/${order.id}`} className="order-action-btn order-action-qr" onClick={(e) => e.stopPropagation()}>
                              <i className="bi bi-qr-code" /> Show QR code
                            </Link>
                          )}
                          <button
                            type="button"
                            className="order-action-btn order-action-cancel"
                            onClick={(e) => {
                              e.stopPropagation();
                              setPendingCancel(order);
                            }}
                          >
                            Cancel order
                          </button>
                        </div>
                      ) : (
                        <div className="order-ticket-actions">
                          <button
                            type="button"
                            className="order-action-btn order-action-reorder"
                            disabled={reorderingOrderId === order.id}
                            onClick={(e) => {
                              e.stopPropagation();
                              void requestReorder(order);
                            }}
                          >
                            {reorderingOrderId === order.id ? <span className="spinner-border spinner-border-sm" role="status" /> : <i className="bi bi-arrow-repeat" />}
                            Order again
                          </button>
                        </div>
                      )}
                    </div>

                    <div className="order-ticket-seam" />

                    <div className="order-ticket-stub">
                      <span className="order-ticket-stub-label">Order</span>
                      <span className="order-ticket-stub-number">#{String(order.orderNumber).padStart(3, "0")}</span>
                      <span className="order-ticket-stub-hint">Show at pickup</span>
                    </div>
                  </article>
                );
              })}
            </div>
            <Pagination currentPage={paged.pageIndex} totalPages={paged.totalPages} onPageChange={(p) => void changePage(p)} />
          </>
        )}
      </section>

      <ConfirmDialog
        isOpen={pendingCancel !== null}
        title="Cancel order?"
        message={pendingCancel ? `Order #${String(pendingCancel.orderNumber).padStart(3, "0")} at ${pendingCancel.businessName} will be cancelled. This can't be undone.` : ""}
        confirmLabel="Cancel order"
        busy={cancelling}
        onConfirm={() => void confirmCancel()}
        onCancel={() => setPendingCancel(null)}
      />

      <OrderDetailModal
        order={selectedOrder}
        pickupLabel={selectedOrder ? formatOrderPickupWindow(selectedOrder.lines, timeZone) : ""}
        onClose={() => setSelectedOrder(null)}
      />

      <ConfirmDialog
        isOpen={pendingReorder !== null}
        title="Start a new basket?"
        message={pendingReorder ? `Your basket has items from ${cart.businessName}. Reordering from ${pendingReorder.businessName} will clear it and start a new one.` : ""}
        confirmLabel="Start new basket"
        cancelLabel="Keep current basket"
        confirmClass="btn-primary"
        busy={reorderingOrderId !== null}
        onConfirm={() => void confirmReorder()}
        onCancel={() => setPendingReorder(null)}
      />
    </>
  );
}

export default Orders;
