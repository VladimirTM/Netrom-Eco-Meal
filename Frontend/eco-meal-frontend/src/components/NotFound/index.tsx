import { Link } from "react-router-dom";

function NotFound() {
  return (
    <div className="text-center py-5 mx-auto" style={{ maxWidth: 420, marginTop: "4rem" }}>
      <div className="em-empty-icon">
        <i className="bi bi-signpost-2" />
      </div>
      <h1 className="h4 fw-bold mb-2">Page not found</h1>
      <p className="text-muted mb-4">The page you're looking for doesn't exist or may have moved.</p>
      <Link to="/" className="btn btn-outline-primary px-4">
        Back to home
      </Link>
    </div>
  );
}

export default NotFound;
