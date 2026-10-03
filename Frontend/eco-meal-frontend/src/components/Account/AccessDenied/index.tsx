import ForbiddenPanel from "../../common/ForbiddenPanel";

function AccessDenied() {
  return (
    <div className="mx-auto" style={{ maxWidth: 420, marginTop: "4rem" }}>
      <ForbiddenPanel message="Your account doesn't have permission to view this page." backHref="/" backLabel="Back to home" />
    </div>
  );
}

export default AccessDenied;
