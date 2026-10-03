import type { OrderDto } from "../../../api/models/Order";
import { formatCurrency } from "../../../utils/currency";
import { orderStatusCssSuffix, orderStatusLabel } from "../../../utils/orderStatus";
import { paymentStatusBadgeClass, paymentStatusIconClass, paymentStatusLabel } from "../../../utils/paymentStatus";
import ImpactEquivalencyStats from "../ImpactEquivalencyStats";

interface OrderDetailModalProps {
  order: OrderDto | null;
  pickupLabel: string;
  onClose: () => void;
}

// Ports OrderDetailModal.razor — reuses the package-modal shell (.pkg-modal-facts etc.) under its
// own .order-modal namespace, same shell, different entity.
function OrderDetailModal({ order, pickupLabel, onClose }: OrderDetailModalProps) {
  if (!order) return null;

  const subtotal = order.lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0);
  const charged = order.payment?.amount ?? subtotal;
  const kgSaved = order.lines.reduce((sum, l) => sum + l.quantity * l.weightKg, 0);

  return (
    <>
      <div className="order-modal-backdrop" onClick={onClose} />
      <div className="order-modal" role="dialog" aria-modal="true" aria-labelledby="order-modal-title">
        <button type="button" className="biz-modal-close" onClick={onClose} aria-label="Close">
          <i className="bi bi-x-lg" />
        </button>

        <div className="order-modal-body">
          <div className="order-modal-top">
            <div>
              <span className="biz-modal-eyebrow">Order #{String(order.orderNumber).padStart(3, "0")}</span>
              <h2 className="pkg-modal-title" id="order-modal-title">
                {order.businessName}
              </h2>
            </div>
            <span className={`order-status-badge order-status-${orderStatusCssSuffix(order.status)}`}>{orderStatusLabel(order.status)}</span>
          </div>

          {order.payment && (
            <span className={`badge ${paymentStatusBadgeClass(order.payment.status)} mb-3`}>
              <i className={`bi ${paymentStatusIconClass(order.payment.status)}`} /> {paymentStatusLabel(order.payment.status)}{" "}
              {formatCurrency(order.payment.amount)}
            </span>
          )}

          <div className="pkg-modal-facts">
            <div className="pkg-modal-fact">
              <i className="bi bi-geo-alt" />
              <div>
                <div className="pkg-modal-fact-label">Pickup window</div>
                <div className="pkg-modal-fact-value">{pickupLabel}</div>
              </div>
            </div>
            <div className="pkg-modal-fact">
              <i className="bi bi-person" />
              <div>
                <div className="pkg-modal-fact-label">Customer</div>
                <div className="pkg-modal-fact-value">{order.customerName}</div>
              </div>
            </div>
          </div>

          {order.logisticsNote && (
            <div className="pkg-modal-fact">
              <i className="bi bi-chat-left-text" />
              <div>
                <div className="pkg-modal-fact-label">Your note</div>
                <div className="pkg-modal-fact-value">{order.logisticsNote}</div>
              </div>
            </div>
          )}

          <div className="biz-modal-divider" />

          <div className="biz-modal-section-title">
            <i className="bi bi-bag" /> Items
          </div>
          <div className="order-ticket-items">
            {order.lines.map((line) => (
              <div className="order-ticket-line" key={line.packageId}>
                <span className="order-ticket-line-qty">{line.quantity}&times;</span>
                <span className="order-ticket-line-name">{line.packageName}</span>
                <span className="order-ticket-line-price">{formatCurrency(line.quantity * line.unitPrice)}</span>
              </div>
            ))}
          </div>

          <div className="order-modal-total">
            <span>Total</span>
            <span className="order-ticket-total-value">
              {formatCurrency(charged)}
              {order.payment && charged < subtotal && <span className="text-muted small fw-normal"> ({formatCurrency(subtotal)} before discount)</span>}
            </span>
          </div>

          {order.status === "Completed" && <ImpactEquivalencyStats kgSaved={kgSaved} />}
        </div>
      </div>
    </>
  );
}

export default OrderDetailModal;
