import { Outlet } from "react-router-dom";

// Chrome-free layout used by the standalone account pages (Login/Register/...).
function EmptyLayout() {
  return <Outlet />;
}

export default EmptyLayout;
