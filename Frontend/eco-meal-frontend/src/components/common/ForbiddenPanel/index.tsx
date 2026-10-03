import { Link } from "react-router-dom";

interface ForbiddenPanelProps {
  message: string;
  backHref?: string;
  backLabel?: string;
}

function ForbiddenPanel({ message, backHref = "/", backLabel = "Back" }: ForbiddenPanelProps) {
  return (
    <div className="text-center py-5">
      <div className="em-empty-icon">
        <i className="bi bi-shield-lock" />
      </div>
      <h1 className="h4 fw-bold mb-2">Access denied</h1>
      <p className="text-muted mb-4">{message}</p>
      <Link to={backHref} className="btn btn-outline-primary px-4">
        {backLabel}
      </Link>
    </div>
  );
}

export default ForbiddenPanel;
