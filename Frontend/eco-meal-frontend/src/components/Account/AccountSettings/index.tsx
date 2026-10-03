import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../../../context/AuthContext/auth-context";
import { authApi } from "../../../api/clients/AuthApiClient";
import { ApiError } from "../../../api/base/http";

function AccountSettings() {
  const { user, logout, refreshUser } = useAuth();
  const navigate = useNavigate();

  const [name, setName] = useState(user?.name ?? "");
  const [savingName, setSavingName] = useState(false);
  const [nameError, setNameError] = useState<string | null>(null);
  const [nameSaved, setNameSaved] = useState(false);

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [savingPassword, setSavingPassword] = useState(false);
  const [passwordError, setPasswordError] = useState<string | null>(null);

  async function handleSaveName(e: FormEvent) {
    e.preventDefault();
    setSavingName(true);
    setNameError(null);
    setNameSaved(false);

    try {
      await authApi.updateName(name);
      await refreshUser();
      setNameSaved(true);
    } catch (err) {
      setNameError(err instanceof ApiError ? err.message : "Something went wrong. Please try again.");
    } finally {
      setSavingName(false);
    }
  }

  async function handleChangePassword(e: FormEvent) {
    e.preventDefault();
    setPasswordError(null);

    if (newPassword !== confirmPassword) {
      setPasswordError("New password and confirmation don't match.");
      return;
    }

    setSavingPassword(true);
    try {
      await authApi.changePassword(currentPassword, newPassword);
      // Changing the password rotates the Identity security stamp (D3) — the current JWT is
      // rejected by the next request, so sign out and ask for a fresh login rather than leave
      // the UI sitting on a token the backend will bounce.
      logout();
      navigate("/account/login?info=" + encodeURIComponent("Password changed — please sign in again."), {
        replace: true,
      });
    } catch (err) {
      setPasswordError(err instanceof ApiError ? err.message : "Something went wrong. Please try again.");
    } finally {
      setSavingPassword(false);
    }
  }

  return (
    <div className="account-settings-page">
      <h1 className="h3 fw-bold mb-1">Account settings</h1>
      <p className="text-muted small mb-4">Update your display name or change your password.</p>

      <div className="card border-0 shadow-sm mb-4">
        <div className="card-body p-4">
          <h2 className="h5 fw-bold mb-3">Profile</h2>

          <div className="mb-3">
            <label className="form-label small text-muted mb-1">Email</label>
            <input className="form-control" value={user?.email ?? ""} disabled />
          </div>

          <form onSubmit={handleSaveName}>
            <div className="mb-3">
              <label htmlFor="displayName" className="form-label small text-muted mb-1">Display name</label>
              <input
                id="displayName"
                className="form-control"
                value={name}
                onChange={(e) => setName(e.target.value)}
                required
                maxLength={100}
              />
            </div>

            {nameError && <div className="alert alert-danger py-2 small">{nameError}</div>}
            {nameSaved && (
              <div className="alert alert-success py-2 small">
                <i className="bi bi-check-circle-fill" /> Name updated.
              </div>
            )}

            <button type="submit" className="btn btn-primary" disabled={savingName}>
              {savingName && <span className="spinner-border spinner-border-sm me-1" role="status" />}
              Save name
            </button>
          </form>
        </div>
      </div>

      <div className="card border-0 shadow-sm">
        <div className="card-body p-4">
          <h2 className="h5 fw-bold mb-3">Change password</h2>

          <form onSubmit={handleChangePassword}>
            <div className="mb-3">
              <label htmlFor="currentPassword" className="form-label small text-muted mb-1">Current password</label>
              <input
                id="currentPassword"
                type="password"
                className="form-control"
                autoComplete="current-password"
                required
                value={currentPassword}
                onChange={(e) => setCurrentPassword(e.target.value)}
              />
            </div>
            <div className="mb-3">
              <label htmlFor="newPassword" className="form-label small text-muted mb-1">New password</label>
              <input
                id="newPassword"
                type="password"
                className="form-control"
                autoComplete="new-password"
                required
                minLength={8}
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
              />
            </div>
            <div className="mb-3">
              <label htmlFor="confirmPassword" className="form-label small text-muted mb-1">Confirm new password</label>
              <input
                id="confirmPassword"
                type="password"
                className="form-control"
                autoComplete="new-password"
                required
                minLength={8}
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
              />
            </div>

            {passwordError && <div className="alert alert-danger py-2 small">{passwordError}</div>}

            <button type="submit" className="btn btn-primary" disabled={savingPassword}>
              {savingPassword ? "Changing…" : "Change password"}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}

export default AccountSettings;
