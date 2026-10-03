import { useState } from "react";

interface ReportDialogProps {
  isOpen: boolean;
  targetLabel: string;
  title?: string;
  message?: string;
  placeholder?: string;
  confirmLabel?: string;
  busy?: boolean;
  error?: string | null;
  onSubmit: (reason: string) => void;
  onCancel: () => void;
}

// Ports ReportDialog.razor — small modal collecting a free-text reason.
function ReportDialog({
  isOpen,
  targetLabel,
  title,
  message = "Tell us what's wrong — an admin will review it.",
  placeholder = "What's the issue?",
  confirmLabel = "Submit report",
  busy = false,
  error = null,
  onSubmit,
  onCancel,
}: ReportDialogProps) {
  const [reason, setReason] = useState("");
  // React's documented "adjusting state when a prop changes" pattern — resets the draft the
  // moment the dialog closes, without a useEffect render-after-render roundtrip.
  const [prevIsOpen, setPrevIsOpen] = useState(isOpen);
  if (isOpen !== prevIsOpen) {
    setPrevIsOpen(isOpen);
    if (!isOpen) setReason("");
  }

  if (!isOpen) return null;

  return (
    <>
      <div className="confirm-backdrop" onClick={onCancel} />
      <div className="confirm-dialog" role="dialog" aria-modal="true" aria-labelledby="report-dialog-title">
        <h2 className="confirm-dialog-title" id="report-dialog-title">
          {title ?? `Report ${targetLabel}`}
        </h2>
        <p className="confirm-dialog-message">{message}</p>
        <textarea
          className="form-control mb-2"
          rows={3}
          maxLength={500}
          placeholder={placeholder}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
        />
        {error ? (
          <p className="text-danger small mb-0">{error}</p>
        ) : (
          reason.trim() === "" && <p className="text-muted small mb-0">A reason is required.</p>
        )}
        <div className="confirm-dialog-actions">
          <button type="button" className="btn btn-outline-secondary" disabled={busy} onClick={onCancel}>
            Cancel
          </button>
          <button type="button" className="btn btn-danger" disabled={busy || reason.trim() === ""} onClick={() => onSubmit(reason)}>
            {busy && <span className="spinner-border spinner-border-sm me-2" role="status" />}
            {confirmLabel}
          </button>
        </div>
      </div>
    </>
  );
}

export default ReportDialog;
