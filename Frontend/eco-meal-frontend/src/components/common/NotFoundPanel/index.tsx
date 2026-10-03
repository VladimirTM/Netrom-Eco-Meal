import { Link } from "react-router-dom";

interface NotFoundPanelProps {
  title?: string;
  message: string;
  backHref?: string;
  backLabel?: string;
}

// Ports NotFoundPanel.razor — used by detail pages when the requested entity no longer exists
// (distinct from the global 404 route / NotFound page).
function NotFoundPanel({ title = "Not found", message, backHref = "/", backLabel = "Back" }: NotFoundPanelProps) {
  return (
    <div className="text-center py-5">
      <div className="em-empty-icon">
        <i className="bi bi-search" />
      </div>
      <h1 className="h4 fw-bold mb-2">{title}</h1>
      <p className="text-muted mb-4">{message}</p>
      <Link to={backHref} className="btn btn-outline-primary px-4">
        {backLabel}
      </Link>
    </div>
  );
}

export default NotFoundPanel;
