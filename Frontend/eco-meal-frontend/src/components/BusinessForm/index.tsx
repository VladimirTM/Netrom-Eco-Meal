import { useCallback, useEffect, useState, type ChangeEvent, type FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { ApiError } from "../../api/base/http";
import { businessesApi, type BusinessWriteRequest } from "../../api/clients/BusinessesApiClient";
import { businessTypesApi } from "../../api/clients/BusinessTypesApiClient";
import { brandsApi } from "../../api/clients/BrandsApiClient";
import { uploadsApi } from "../../api/clients/UploadsApiClient";
import type { BusinessClosureDto, BusinessHoursDto, DayOfWeekName } from "../../api/models/Business";
import type { BrandDto, BusinessTypeDto } from "../../api/models/Lookup";
import { useAuth } from "../../context/AuthContext/auth-context";
import { useGeolocation } from "../../hooks/useGeolocation";
import ConfirmDialog from "../common/ConfirmDialog";
import ForbiddenPanel from "../common/ForbiddenPanel";
import NotFoundPanel from "../common/NotFoundPanel";
import { WEEK_ORDER } from "../../utils/businessHoursStatus";
import { formatDateOnly } from "../../utils/dates";

// Image size limit mirrors Constants/ImageUpload.MaxSizeBytes — checked client-side first so a
// too-large file never starts a doomed upload.
const MAX_IMAGE_BYTES = 5 * 1024 * 1024;

interface HoursRowState {
  dayOfWeek: DayOfWeekName;
  isClosed: boolean;
  // "HH:mm", the <input type="time"> shape — converted to/from the API's "HH:mm:ss" at the edges.
  openTime: string;
  closeTime: string;
}

// Always returns all 7 days, filling in a 09:00–18:00 default for any day not yet configured.
function buildWeekRows(existing: BusinessHoursDto[]): HoursRowState[] {
  return WEEK_ORDER.map((day) => {
    const found = existing.find((h) => h.dayOfWeek === day);
    return {
      dayOfWeek: day,
      isClosed: found?.isClosed ?? false,
      openTime: found?.openTime ? found.openTime.slice(0, 5) : "09:00",
      closeTime: found?.closeTime ? found.closeTime.slice(0, 5) : "18:00",
    };
  });
}

interface FieldErrors {
  name?: string;
  description?: string;
  address?: string;
  latitude?: string;
  longitude?: string;
  loyaltyPunchThreshold?: string;
  loyaltyDiscountAmount?: string;
}

// Ports BusinessForm.razor (/businesses/create, /businesses/edit/:id). Create is admin-only;
// edit is usable by the admin or by a manager on that specific business's staff — enforced by the
// API (a non-staff manager's PUT /businesses/:id 403s), confirmed client-side first via
// GET /businesses/:id/staff so a non-staff manager sees ForbiddenPanel immediately rather than
// after filling out the whole form.
function BusinessForm() {
  const { id } = useParams<{ id?: string }>();
  const isEdit = id !== undefined;
  const navigate = useNavigate();
  const { user } = useAuth();
  const isAdmin = user?.role === "Admin";
  const { locate, locating, error: locationError } = useGeolocation();

  const [loading, setLoading] = useState(true);
  const [forbidden, setForbidden] = useState(false);
  const [notFound, setNotFound] = useState(false);

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [address, setAddress] = useState("");
  const [imageUrl, setImageUrl] = useState("");
  const [latitude, setLatitude] = useState<number | null>(null);
  const [longitude, setLongitude] = useState<number | null>(null);
  const [businessTypeId, setBusinessTypeId] = useState("");
  const [brandId, setBrandId] = useState("");
  const [loyaltyPunchThreshold, setLoyaltyPunchThreshold] = useState<number | null>(null);
  const [loyaltyDiscountAmount, setLoyaltyDiscountAmount] = useState<number | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  const [businessTypes, setBusinessTypes] = useState<BusinessTypeDto[]>([]);
  const [brands, setBrands] = useState<BrandDto[]>([]);

  const [uploadingImage, setUploadingImage] = useState(false);
  const [imageUploadError, setImageUploadError] = useState<string | null>(null);

  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const [hoursRows, setHoursRows] = useState<HoursRowState[]>([]);
  const [savingHours, setSavingHours] = useState(false);
  const [hoursError, setHoursError] = useState<string | null>(null);
  const [hoursSavedMessage, setHoursSavedMessage] = useState<string | null>(null);

  const [closures, setClosures] = useState<BusinessClosureDto[]>([]);
  const [closureStart, setClosureStart] = useState("");
  const [closureEnd, setClosureEnd] = useState("");
  const [closureReason, setClosureReason] = useState("");
  const [addingClosure, setAddingClosure] = useState(false);
  const [closureError, setClosureError] = useState<string | null>(null);
  const [pendingRemoveClosure, setPendingRemoveClosure] = useState<BusinessClosureDto | null>(null);
  const [removingClosure, setRemovingClosure] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);

    const [types, brandList] = await Promise.all([businessTypesApi.getAll(), brandsApi.getAll()]);
    setBusinessTypes(types);
    setBrands(brandList);

    if (!isEdit || !id) {
      if (!isAdmin) {
        setForbidden(true);
        setLoading(false);
        return;
      }
      // InputSelect only syncs its bound value once the user changes the selection, so without a
      // default businessTypeId stays "" and the create request fails its FK constraint.
      if (types.length > 0) setBusinessTypeId(types[0].id);
      setLoading(false);
      return;
    }

    try {
      const existing = await businessesApi.getById(id);

      if (!isAdmin) {
        const staff = await businessesApi.getStaff(id);
        if (!staff.some((s) => s.id === user?.id)) {
          setForbidden(true);
          setLoading(false);
          return;
        }
      }

      setName(existing.name);
      setDescription(existing.description);
      setAddress(existing.address);
      setImageUrl(existing.imageUrl ?? "");
      setLatitude(existing.latitude);
      setLongitude(existing.longitude);
      setBusinessTypeId(existing.businessTypeId);
      setBrandId(existing.brandId ?? "");
      setLoyaltyPunchThreshold(existing.loyaltyPunchThreshold);
      setLoyaltyDiscountAmount(existing.loyaltyDiscountAmount);
      setHoursRows(buildWeekRows(existing.hours));
      setClosures([...existing.closures].sort((a, b) => a.startDate.localeCompare(b.startDate)));
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) setNotFound(true);
      else if (err instanceof ApiError && err.status === 403) setForbidden(true);
      else throw err;
    } finally {
      setLoading(false);
    }
  }, [isEdit, isAdmin, id, user?.id]);

  useEffect(() => {
    // Synchronizes with the server once on mount (and if the route's :id or signed-in user changes).
    // oxlint-disable-next-line react/set-state-in-effect
    load();
  }, [load]);

  function validate(): FieldErrors {
    const errors: FieldErrors = {};
    if (!name.trim()) errors.name = "Enter a business name.";
    if (!description.trim()) errors.description = "Enter a short description.";
    if (!address.trim()) errors.address = "Enter an address.";
    if (latitude !== null && (latitude < -90 || latitude > 90)) errors.latitude = "Latitude must be between -90 and 90.";
    if (longitude !== null && (longitude < -180 || longitude > 180)) errors.longitude = "Longitude must be between -180 and 180.";
    if (loyaltyPunchThreshold !== null && (loyaltyPunchThreshold < 2 || loyaltyPunchThreshold > 50)) errors.loyaltyPunchThreshold = "Threshold must be between 2 and 50 orders.";
    if (loyaltyDiscountAmount !== null && (loyaltyDiscountAmount < 0.5 || loyaltyDiscountAmount > 1000)) errors.loyaltyDiscountAmount = "Discount must be at least 0.5 lei.";
    return errors;
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const errors = validate();
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    const payload: BusinessWriteRequest = {
      name: name.trim(),
      description: description.trim(),
      address: address.trim(),
      imageUrl: imageUrl.trim() || null,
      latitude,
      longitude,
      businessTypeId,
      brandId: brandId || null,
      loyaltyPunchThreshold,
      loyaltyDiscountAmount,
    };

    setSubmitting(true);
    setSubmitError(null);
    try {
      if (isEdit && id) await businessesApi.update(id, payload);
      else await businessesApi.create(payload);
      navigate("/businesses");
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.message : "Couldn't save this business right now. Please try again.");
    } finally {
      setSubmitting(false);
    }
  }

  async function onImageSelected(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;

    setImageUploadError(null);
    if (file.size > MAX_IMAGE_BYTES) {
      setImageUploadError(`Image is too large — max ${MAX_IMAGE_BYTES / 1024 / 1024} MB.`);
      return;
    }

    setUploadingImage(true);
    try {
      const url = await uploadsApi.upload("businesses", file);
      setImageUrl(url);
    } catch (err) {
      setImageUploadError(err instanceof ApiError ? err.message : "Couldn't upload that image. Please try again.");
    } finally {
      setUploadingImage(false);
    }
  }

  async function useCurrentLocation() {
    const position = await locate();
    if (position) {
      setLatitude(position.lat);
      setLongitude(position.lng);
    }
  }

  function updateHoursRow(day: DayOfWeekName, patch: Partial<HoursRowState>) {
    setHoursRows((rows) => rows.map((r) => (r.dayOfWeek === day ? { ...r, ...patch } : r)));
  }

  async function saveHours() {
    if (!id) return;
    setHoursError(null);
    setHoursSavedMessage(null);

    const incomplete = hoursRows.find((r) => !r.isClosed && (!r.openTime || !r.closeTime));
    if (incomplete) {
      setHoursError(`Set both an open and close time for ${incomplete.dayOfWeek}, or mark it closed.`);
      return;
    }

    setSavingHours(true);
    try {
      await businessesApi.setHours(
        id,
        hoursRows.map((r) => ({
          dayOfWeek: r.dayOfWeek,
          isClosed: r.isClosed,
          openTime: r.isClosed ? null : `${r.openTime}:00`,
          closeTime: r.isClosed ? null : `${r.closeTime}:00`,
        })),
      );
      setHoursSavedMessage("Hours saved.");
    } catch (err) {
      setHoursError(err instanceof ApiError && err.status === 403 ? "You don't have permission to edit this business's hours." : "Couldn't save hours right now. Please try again.");
    } finally {
      setSavingHours(false);
    }
  }

  async function addClosure() {
    if (!id) return;
    setClosureError(null);

    if (!closureStart || !closureEnd) {
      setClosureError("Pick a start and end date.");
      return;
    }
    if (closureEnd < closureStart) {
      setClosureError("End date must be on or after the start date.");
      return;
    }

    setAddingClosure(true);
    try {
      const closure = await businessesApi.addClosure(id, closureStart, closureEnd, closureReason || null);
      setClosures((prev) => [...prev, closure].sort((a, b) => a.startDate.localeCompare(b.startDate)));
      setClosureStart("");
      setClosureEnd("");
      setClosureReason("");
    } catch (err) {
      setClosureError(err instanceof ApiError && err.status === 403 ? "You don't have permission to edit this business's closures." : "Couldn't add that closure right now. Please try again.");
    } finally {
      setAddingClosure(false);
    }
  }

  async function confirmRemoveClosure() {
    if (!id || !pendingRemoveClosure) return;
    const closureId = pendingRemoveClosure.id;
    setRemovingClosure(true);
    try {
      await businessesApi.removeClosure(id, closureId);
      setClosures((prev) => prev.filter((c) => c.id !== closureId));
    } catch {
      // Matches BusinessForm.razor's own ConfirmRemoveClosureAsync — a failed removal just closes
      // the dialog without surfacing an error; the closure stays in the list to retry later.
    } finally {
      setRemovingClosure(false);
      setPendingRemoveClosure(null);
    }
  }

  if (loading) {
    return (
      <div className="d-flex justify-content-center py-5">
        <div className="spinner-border text-primary" role="status">
          <span className="visually-hidden">Loading...</span>
        </div>
      </div>
    );
  }

  if (forbidden) {
    return <ForbiddenPanel message={isEdit ? "You can only edit your own business." : "Only an admin can create new businesses."} backHref="/businesses" backLabel="Back to businesses" />;
  }

  if (notFound) {
    return <NotFoundPanel title="Business not found" message="This business no longer exists — it may have already been deleted." backHref="/businesses" backLabel="Back to businesses" />;
  }

  return (
    <div>
      <div className="d-flex justify-content-between align-items-start mb-4">
        <div>
          <h1 className="h3 fw-bold mb-1">{isEdit ? "Edit Business" : "Create Business"}</h1>
          <p className="text-muted small mb-0">{isEdit ? "Update the business details" : "Add a new food business to the platform"}</p>
        </div>
        <button type="button" className="btn btn-outline-secondary" onClick={() => navigate("/businesses")}>
          <i className="bi bi-arrow-left me-1" /> Back
        </button>
      </div>

      <div className="row justify-content-center">
        <div className="col-lg-7 col-xl-6">
          <div className="card border-0 shadow-sm">
            <div className="card-body p-4">
              <form onSubmit={handleSubmit}>
                <div className="mb-3">
                  <label htmlFor="name" className="form-label fw-semibold">
                    Name
                  </label>
                  <input id="name" className="form-control" placeholder="e.g. Green Bites" value={name} onChange={(e) => setName(e.target.value)} />
                  {fieldErrors.name && <div className="validation-message small d-block mt-1">{fieldErrors.name}</div>}
                </div>
                <div className="mb-3">
                  <label htmlFor="description" className="form-label fw-semibold">
                    Description
                  </label>
                  <input id="description" className="form-control" placeholder="Short description of the business" value={description} onChange={(e) => setDescription(e.target.value)} />
                  {fieldErrors.description && <div className="validation-message small d-block mt-1">{fieldErrors.description}</div>}
                </div>
                <div className="row g-3 mb-3">
                  <div className="col-sm-7">
                    <label htmlFor="address" className="form-label fw-semibold">
                      Address
                    </label>
                    <input id="address" className="form-control" placeholder="Street, City" value={address} onChange={(e) => setAddress(e.target.value)} />
                    {fieldErrors.address && <div className="validation-message small d-block mt-1">{fieldErrors.address}</div>}
                  </div>
                  <div className="col-sm-5">
                    <label htmlFor="businessType" className="form-label fw-semibold">
                      Type
                    </label>
                    <select id="businessType" className="form-select" value={businessTypeId} onChange={(e) => setBusinessTypeId(e.target.value)}>
                      {businessTypes.map((type) => (
                        <option value={type.id} key={type.id}>
                          {type.name}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>
                <div className="mb-3">
                  <label htmlFor="brand" className="form-label fw-semibold">
                    Brand <span className="text-muted fw-normal">(optional — groups this as one location of a chain)</span>
                  </label>
                  <select id="brand" className="form-select" value={brandId} onChange={(e) => setBrandId(e.target.value)}>
                    <option value="">— Standalone business —</option>
                    {brands.map((brand) => (
                      <option value={brand.id} key={brand.id}>
                        {brand.name}
                      </option>
                    ))}
                  </select>
                  {isAdmin ? (
                    <div className="form-text">
                      Manage the list of brands from the <a href="/types">Kitchen &amp; Package Types</a> page.
                    </div>
                  ) : (
                    <div className="form-text">Don't see the chain you're looking for? Ask an admin to add it.</div>
                  )}
                </div>
                <div className="mb-4">
                  <label htmlFor="imageFile" className="form-label fw-semibold">
                    Photo <span className="text-muted fw-normal">(optional)</span>
                  </label>
                  <div className="d-flex align-items-start gap-3">
                    {imageUrl.trim() !== "" && <img src={imageUrl} alt="Business photo preview" className="form-image-preview" />}
                    <div className="flex-grow-1">
                      <input id="imageFile" type="file" className="form-control" accept="image/*" onChange={onImageSelected} disabled={uploadingImage} />
                      <div className="form-text">Or paste an image URL directly:</div>
                      <input className="form-control form-control-sm mt-1" placeholder="https://..." value={imageUrl} onChange={(e) => setImageUrl(e.target.value)} />
                      {uploadingImage && (
                        <div className="text-muted small mt-1">
                          <span className="spinner-border spinner-border-sm me-1" role="status" />
                          Uploading…
                        </div>
                      )}
                      {imageUploadError && <div className="text-danger small mt-1">{imageUploadError}</div>}
                    </div>
                  </div>
                </div>
                <div className="mb-4">
                  <label className="form-label fw-semibold">
                    Location <span className="text-muted fw-normal">(optional — powers "near me" sort and the map view)</span>
                  </label>
                  <div className="row g-3">
                    <div className="col-sm-5">
                      <input
                        type="number"
                        className="form-control"
                        placeholder="Latitude"
                        value={latitude ?? ""}
                        onChange={(e) => setLatitude(e.target.value === "" ? null : Number(e.target.value))}
                      />
                      {fieldErrors.latitude && <div className="validation-message small d-block mt-1">{fieldErrors.latitude}</div>}
                    </div>
                    <div className="col-sm-5">
                      <input
                        type="number"
                        className="form-control"
                        placeholder="Longitude"
                        value={longitude ?? ""}
                        onChange={(e) => setLongitude(e.target.value === "" ? null : Number(e.target.value))}
                      />
                      {fieldErrors.longitude && <div className="validation-message small d-block mt-1">{fieldErrors.longitude}</div>}
                    </div>
                    <div className="col-sm-2">
                      <button type="button" className="btn btn-outline-secondary w-100" title="Use my current location" disabled={locating} onClick={useCurrentLocation}>
                        {locating ? <span className="spinner-border spinner-border-sm" role="status" /> : <i className="bi bi-geo-alt" />}
                      </button>
                    </div>
                  </div>
                  {locationError && <div className="text-danger small mt-1">{locationError}</div>}
                </div>
                <div className="mb-4">
                  <label className="form-label fw-semibold">
                    Loyalty punch card <span className="text-muted fw-normal">(optional — rewards a customer for coming back)</span>
                  </label>
                  <div className="row g-3">
                    <div className="col-sm-6">
                      <div className="input-group">
                        <span className="input-group-text">Every</span>
                        <input
                          type="number"
                          className="form-control"
                          placeholder="e.g. 6"
                          value={loyaltyPunchThreshold ?? ""}
                          onChange={(e) => setLoyaltyPunchThreshold(e.target.value === "" ? null : Number(e.target.value))}
                        />
                        <span className="input-group-text">orders/month</span>
                      </div>
                      {fieldErrors.loyaltyPunchThreshold && <div className="validation-message small d-block mt-1">{fieldErrors.loyaltyPunchThreshold}</div>}
                    </div>
                    <div className="col-sm-6">
                      <div className="input-group">
                        <span className="input-group-text">gets</span>
                        <input
                          type="number"
                          className="form-control"
                          placeholder="e.g. 2"
                          value={loyaltyDiscountAmount ?? ""}
                          onChange={(e) => setLoyaltyDiscountAmount(e.target.value === "" ? null : Number(e.target.value))}
                        />
                        <span className="input-group-text">lei off</span>
                      </div>
                      {fieldErrors.loyaltyDiscountAmount && <div className="validation-message small d-block mt-1">{fieldErrors.loyaltyDiscountAmount}</div>}
                    </div>
                  </div>
                  <div className="form-text">Leave both blank to turn the punch card off. Applied automatically at checkout — no code needed.</div>
                </div>
                {submitError && <div className="alert alert-danger py-2 small">{submitError}</div>}
                <button type="submit" className="btn btn-primary w-100 py-2" disabled={submitting}>
                  {submitting && <span className="spinner-border spinner-border-sm me-2" role="status" />}
                  <i className="bi bi-check-lg me-1" /> {isEdit ? "Save Changes" : "Create Business"}
                </button>
              </form>
            </div>
          </div>
        </div>
      </div>

      {isEdit && (
        <>
          <div className="row justify-content-center mt-4">
            <div className="col-lg-7 col-xl-6">
              <div className="card border-0 shadow-sm">
                <div className="card-body p-4">
                  <h2 className="h5 fw-bold mb-1">Opening Hours</h2>
                  <p className="text-muted small mb-3">Powers the "closed now" indicator customers see on your kitchen's card and page.</p>

                  {hoursError && <div className="alert alert-danger py-2 small">{hoursError}</div>}
                  {hoursSavedMessage && <div className="alert alert-success py-2 small">{hoursSavedMessage}</div>}

                  {hoursRows.map((row) => (
                    <div className="row g-2 align-items-center mb-2" key={row.dayOfWeek}>
                      <div className="col-3 small fw-semibold">{row.dayOfWeek.slice(0, 3)}</div>
                      <div className="col-3">
                        <div className="form-check">
                          <input
                            className="form-check-input"
                            type="checkbox"
                            id={`closed-${row.dayOfWeek}`}
                            checked={row.isClosed}
                            onChange={(e) => updateHoursRow(row.dayOfWeek, { isClosed: e.target.checked })}
                          />
                          <label className="form-check-label small" htmlFor={`closed-${row.dayOfWeek}`}>
                            Closed
                          </label>
                        </div>
                      </div>
                      <div className="col-3">
                        <input
                          type="time"
                          className="form-control form-control-sm"
                          value={row.openTime}
                          disabled={row.isClosed}
                          onChange={(e) => updateHoursRow(row.dayOfWeek, { openTime: e.target.value })}
                        />
                      </div>
                      <div className="col-3">
                        <input
                          type="time"
                          className="form-control form-control-sm"
                          value={row.closeTime}
                          disabled={row.isClosed}
                          onChange={(e) => updateHoursRow(row.dayOfWeek, { closeTime: e.target.value })}
                        />
                      </div>
                    </div>
                  ))}

                  <button type="button" className="btn btn-outline-primary btn-sm mt-2" disabled={savingHours} onClick={saveHours}>
                    {savingHours && <span className="spinner-border spinner-border-sm me-1" role="status" />}
                    Save hours
                  </button>
                </div>
              </div>
            </div>
          </div>

          <div className="row justify-content-center mt-4">
            <div className="col-lg-7 col-xl-6">
              <div className="card border-0 shadow-sm">
                <div className="card-body p-4">
                  <h2 className="h5 fw-bold mb-1">Holiday Closures</h2>
                  <p className="text-muted small mb-3">Overrides your weekly hours for a date range — e.g. a vacation or a one-off closure.</p>

                  {closureError && <div className="alert alert-danger py-2 small">{closureError}</div>}

                  {closures.length > 0 ? (
                    <ul className="list-group list-group-flush mb-3">
                      {closures.map((closure) => (
                        <li className="list-group-item d-flex justify-content-between align-items-center px-0" key={closure.id}>
                          <div>
                            <div className="small fw-semibold">
                              {formatDateOnly(closure.startDate)} – {formatDateOnly(closure.endDate)}
                            </div>
                            {closure.reason && <div className="text-muted small">{closure.reason}</div>}
                          </div>
                          <button type="button" className="btn btn-sm btn-outline-danger" title="Remove closure" onClick={() => setPendingRemoveClosure(closure)}>
                            <i className="bi bi-trash" />
                          </button>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p className="text-muted small">No holiday closures scheduled.</p>
                  )}

                  <div className="row g-2">
                    <div className="col-sm-4">
                      <label className="form-label small">Start date</label>
                      <input type="date" className="form-control form-control-sm" value={closureStart} onChange={(e) => setClosureStart(e.target.value)} />
                    </div>
                    <div className="col-sm-4">
                      <label className="form-label small">End date</label>
                      <input type="date" className="form-control form-control-sm" value={closureEnd} onChange={(e) => setClosureEnd(e.target.value)} />
                    </div>
                    <div className="col-sm-4">
                      <label className="form-label small">
                        Reason <span className="text-muted fw-normal">(optional)</span>
                      </label>
                      <input type="text" className="form-control form-control-sm" placeholder="e.g. Summer vacation" value={closureReason} onChange={(e) => setClosureReason(e.target.value)} />
                    </div>
                  </div>
                  <button type="button" className="btn btn-outline-primary btn-sm mt-3" disabled={addingClosure} onClick={addClosure}>
                    {addingClosure && <span className="spinner-border spinner-border-sm me-1" role="status" />}
                    Add closure
                  </button>
                </div>
              </div>
            </div>
          </div>

          <ConfirmDialog
            isOpen={pendingRemoveClosure !== null}
            title="Remove closure?"
            message={pendingRemoveClosure ? `The closure from ${formatDateOnly(pendingRemoveClosure.startDate)} to ${formatDateOnly(pendingRemoveClosure.endDate)} will be removed. This can't be undone.` : ""}
            confirmLabel="Remove closure"
            busy={removingClosure}
            onConfirm={confirmRemoveClosure}
            onCancel={() => setPendingRemoveClosure(null)}
          />
        </>
      )}
    </div>
  );
}

export default BusinessForm;
