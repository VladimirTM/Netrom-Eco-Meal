import { useEffect, useState } from "react";
import { Navigate, useParams } from "react-router-dom";
import { ApiError } from "../../api/base/http";
import { ordersApi } from "../../api/clients/OrdersApiClient";
import ForbiddenPanel from "../common/ForbiddenPanel";
import NotFoundPanel from "../common/NotFoundPanel";

// Ports OrderValidateLegacy.razor (/orders/validate/:id) — reachable by a QR code printed/saved
// before pickup passes gained their own PassId route segment. Resolves to the single pass a
// pre-migration order has and forwards to OrderValidate, rather than 404ing on a staff member
// scanning a still-valid older ticket.
function OrderValidateLegacy() {
  const { id } = useParams<{ id: string }>();
  const [redirectTo, setRedirectTo] = useState<string | null>(null);
  const [forbidden, setForbidden] = useState(false);
  const [notFound, setNotFound] = useState(false);
  const [ambiguous, setAmbiguous] = useState(false);

  useEffect(() => {
    if (!id) return;
    (async () => {
      try {
        const order = await ordersApi.getForManagementById(id);
        if (order.pickupPasses.length !== 1) {
          setAmbiguous(true);
          return;
        }
        setRedirectTo(`/orders/validate/${id}/${order.pickupPasses[0].id}`);
      } catch (err) {
        if (err instanceof ApiError && err.status === 403) setForbidden(true);
        else setNotFound(true);
      }
    })();
  }, [id]);

  if (redirectTo) return <Navigate to={redirectTo} replace />;

  if (forbidden) {
    return (
      <div className="validate-page">
        <ForbiddenPanel message="You don't manage this order's business." backHref="/orders/manage" backLabel="Back to orders" />
      </div>
    );
  }
  if (notFound) {
    return (
      <div className="validate-page">
        <NotFoundPanel message="This order no longer exists." backHref="/orders/manage" backLabel="Back to orders" />
      </div>
    );
  }
  if (ambiguous) {
    return (
      <div className="validate-page">
        <NotFoundPanel
          message="Couldn't tell which pickup pass this QR code refers to — ask the customer to show their current QR code."
          backHref="/orders/manage"
          backLabel="Back to orders"
        />
      </div>
    );
  }

  return (
    <div className="validate-page">
      <div className="d-flex justify-content-center py-5">
        <div className="spinner-border text-primary" role="status">
          <span className="visually-hidden">Loading...</span>
        </div>
      </div>
    </div>
  );
}

export default OrderValidateLegacy;
