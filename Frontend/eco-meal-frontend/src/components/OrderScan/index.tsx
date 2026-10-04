import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../../api/base/http";
import { ordersApi } from "../../api/clients/OrdersApiClient";
import type { OrderDto } from "../../api/models/Order";
import { useAuth } from "../../context/AuthContext/auth-context";
import { useManagedBusiness } from "../../context/ManagedBusinessContext/managed-business-context";
import { useQrScanner } from "../../hooks/useQrScanner";

// Ports OrderScan.razor (/orders/scan) — camera init is gated behind an explicit tap rather than
// firing on render (required for reliable behavior on iOS Safari), and the camera track is always
// stopped on unmount so the OS camera-in-use indicator doesn't stay lit after leaving this page.
function OrderScan() {
  const { user } = useAuth();
  const isAdmin = user?.role === "Admin";
  const { selectedBusinessId } = useManagedBusiness();

  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const { scanning, starting, cameraError, startCamera, stopCamera } = useQrScanner(videoRef, canvasRef);

  const [manualSearch, setManualSearch] = useState("");
  const [manualBusy, setManualBusy] = useState(false);
  const [manualError, setManualError] = useState<string | null>(null);
  const [manualMatches, setManualMatches] = useState<OrderDto[] | null>(null);

  useEffect(() => stopCamera, [stopCamera]);

  async function manualLookup() {
    if (!manualSearch.trim() || manualBusy) return;

    setManualBusy(true);
    setManualError(null);
    setManualMatches(null);

    // Admin searches every business; a manager is scoped to whichever one they've currently
    // selected in the sidebar switcher (same rule /orders/manage and /payments use).
    const businessId = isAdmin ? undefined : (selectedBusinessId ?? undefined);

    try {
      // Only Confirmed orders have a live, unredeemed pickup pass to confirm — matches what
      // scanning a real QR code would land on.
      const result = await ordersApi.getForManagementPaged(1, 5, manualSearch.trim(), businessId ?? null, "Confirmed");
      if (result.items.length === 0) {
        setManualError("No confirmed order matches that number — check it hasn't already been picked up, or that it's been confirmed yet.");
      } else {
        setManualMatches(result.items);
      }
    } catch (err) {
      setManualError(err instanceof ApiError && (err.status === 401 || err.status === 403) ? "You don't manage a business yet, so there's nothing to look up." : "Couldn't look that order up.");
    } finally {
      setManualBusy(false);
    }
  }

  return (
    <div className="scan-page">
      <div className="scan-header">
        <h1 className="h4 fw-bold mb-1">Scan pickup QR code</h1>
        <p className="scan-hint">Point the camera at the customer's pickup pass to bring up their order.</p>
      </div>

      {cameraError && (
        <div className="alert alert-danger" role="alert">
          {cameraError}
        </div>
      )}

      <div className="scan-viewfinder">
        {/* eslint-disable-next-line jsx-a11y/media-has-caption */}
        <video ref={videoRef} autoPlay playsInline muted />
        <canvas ref={canvasRef} hidden />

        {scanning ? (
          <div className="scan-frame-guide" aria-hidden="true">
            <span className="scan-corner scan-corner-tl" />
            <span className="scan-corner scan-corner-tr" />
            <span className="scan-corner scan-corner-bl" />
            <span className="scan-corner scan-corner-br" />
          </div>
        ) : (
          <div className="scan-idle-overlay">
            <button type="button" className="btn btn-primary px-4" disabled={starting} onClick={startCamera}>
              {starting && <span className="spinner-border spinner-border-sm me-2" role="status" />}
              <i className="bi bi-camera-fill me-1" /> Start scanning
            </button>
          </div>
        )}
      </div>

      <div className="scan-manual">
        <p className="scan-manual-label">No camera, or the code won't scan? Look the order up instead:</p>
        <div className="scan-manual-form">
          <input
            type="text"
            className="form-control"
            placeholder="Order number, e.g. 021"
            value={manualSearch}
            onChange={(e) => setManualSearch(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") manualLookup();
            }}
          />
          <button type="button" className="btn btn-outline-primary" disabled={manualBusy || !manualSearch.trim()} onClick={manualLookup}>
            {manualBusy && <span className="spinner-border spinner-border-sm me-1" role="status" />}
            Find order
          </button>
        </div>

        {manualError && (
          <div className="alert alert-danger py-2 small mt-2" role="alert">
            {manualError}
          </div>
        )}

        {manualMatches && manualMatches.length > 0 && (
          <ul className="scan-manual-results">
            {manualMatches.map((order) => (
              <li key={order.id}>
                <span>
                  Order #{String(order.orderNumber).padStart(3, "0")} — {order.customerName} ({order.businessName})
                </span>
                {order.pickupPasses.length === 1 ? (
                  <Link className="btn btn-sm btn-primary" to={`/orders/validate/${order.id}/${order.pickupPasses[0].id}`}>
                    Confirm pickup
                  </Link>
                ) : (
                  <div className="scan-manual-pass-picker">
                    {[...order.pickupPasses]
                      .sort((a, b) => a.createdAt.localeCompare(b.createdAt))
                      .map((pass) => (
                        <Link className="btn btn-sm btn-outline-primary" to={`/orders/validate/${order.id}/${pass.id}`} key={pass.id}>
                          {pass.label}
                        </Link>
                      ))}
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>

      <Link to="/orders/manage" className="biz-page-back scan-back-link">
        <i className="bi bi-arrow-left" /> Back to order management
      </Link>
    </div>
  );
}

export default OrderScan;
