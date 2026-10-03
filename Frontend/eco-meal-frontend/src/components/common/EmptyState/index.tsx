import type { ReactNode } from "react";

interface EmptyStateProps {
  icon: string;
  message: string;
  children?: ReactNode;
}

// Mirrors the `.em-empty-icon` + muted-message pattern used throughout the Blazor pages
// (Home's "no kitchens", Impact's "no one has opted in yet", etc.).
function EmptyState({ icon, message, children }: EmptyStateProps) {
  return (
    <div className="text-center py-5">
      <div className="em-empty-icon">
        <i className={`bi ${icon}`} />
      </div>
      <p className="text-muted mb-0">{message}</p>
      {children}
    </div>
  );
}

export default EmptyState;
