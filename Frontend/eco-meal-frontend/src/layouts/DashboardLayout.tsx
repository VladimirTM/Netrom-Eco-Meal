import { Outlet } from "react-router-dom";
import NavMenu from "./NavMenu";
import NotificationPanel from "../components/common/NotificationPanel";

// Mirrors Blazor's MainLayout.razor — the Admin/BusinessManager sidebar shell.
function DashboardLayout() {
  return (
    <>
      <div className="page">
        <div className="sidebar">
          <NavMenu />
        </div>
        <main>
          <article className="content px-4 py-4">
            <Outlet />
          </article>
        </main>
      </div>

      {/* Outside .page on purpose — same stacking-context reasoning as PublicLayout's own copy. */}
      <NotificationPanel />
    </>
  );
}

export default DashboardLayout;
