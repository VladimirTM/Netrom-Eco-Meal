interface ConfirmDialogProps {
  isOpen: boolean;
  title?: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  confirmClass?: string;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

// Ports ConfirmDialog.razor — generic confirm/cancel modal.
function ConfirmDialog({
  isOpen,
  title = "Are you sure?",
  message,
  confirmLabel = "Delete",
  cancelLabel = "Cancel",
  confirmClass = "btn-danger",
  busy = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  if (!isOpen) return null;

  return (
    <>
      <div className="confirm-backdrop" onClick={onCancel} />
      <div className="confirm-dialog" role="alertdialog" aria-modal="true" aria-labelledby="confirm-dialog-title">
        <h2 className="confirm-dialog-title" id="confirm-dialog-title">
          {title}
        </h2>
        <p className="confirm-dialog-message">{message}</p>
        <div className="confirm-dialog-actions">
          <button type="button" className="btn btn-outline-secondary" disabled={busy} onClick={onCancel}>
            {cancelLabel}
          </button>
          <button type="button" className={`btn ${confirmClass}`} disabled={busy} onClick={onConfirm}>
            {busy && <span className="spinner-border spinner-border-sm me-2" role="status" />}
            {confirmLabel}
          </button>
        </div>
      </div>
    </>
  );
}

export default ConfirmDialog;
