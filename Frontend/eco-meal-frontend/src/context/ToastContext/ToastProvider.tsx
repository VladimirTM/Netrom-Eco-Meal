import { useCallback, useRef, useState, type ReactNode } from "react";
import { ToastContext, type ToastVariant } from "./toast-context";

interface Toast {
  id: number;
  message: string;
  variant: ToastVariant;
}

const AUTO_DISMISS_MS = 5000;

function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const nextId = useRef(0);

  const showToast = useCallback((message: string, variant: ToastVariant = "info") => {
    const id = nextId.current++;
    setToasts((prev) => [...prev, { id, message, variant }]);
    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
    }, AUTO_DISMISS_MS);
  }, []);

  function dismiss(id: number) {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }

  return (
    <ToastContext.Provider value={{ showToast }}>
      {children}
      <div
        style={{
          position: "fixed",
          bottom: "1rem",
          right: "1rem",
          zIndex: 1080,
          display: "flex",
          flexDirection: "column",
          gap: "0.5rem",
          maxWidth: "min(90vw, 360px)",
        }}
      >
        {toasts.map((t) => (
          <div
            key={t.id}
            className={`alert alert-${t.variant} shadow-sm mb-0 d-flex align-items-start justify-content-between gap-2`}
            role="alert"
          >
            <span>{t.message}</span>
            <button
              type="button"
              className="btn-close"
              aria-label="Dismiss"
              onClick={() => dismiss(t.id)}
            />
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export default ToastProvider;
