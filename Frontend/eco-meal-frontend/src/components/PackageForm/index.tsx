import { useEffect, useState, type ChangeEvent, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError } from "../../api/base/http";
import { aiApi } from "../../api/clients/AiApiClient";
import { businessesApi } from "../../api/clients/BusinessesApiClient";
import { packageTemplatesApi } from "../../api/clients/PackageTemplatesApiClient";
import { packageTypesApi } from "../../api/clients/PackageTypesApiClient";
import { packagesApi, type PackageWriteRequest } from "../../api/clients/PackagesApiClient";
import { uploadsApi } from "../../api/clients/UploadsApiClient";
import type { BusinessDto } from "../../api/models/Business";
import type { PackageTypeDto } from "../../api/models/Lookup";
import { useAuth } from "../../context/AuthContext/auth-context";
import { useManagedBusiness } from "../../context/ManagedBusinessContext/managed-business-context";
import ForbiddenPanel from "../common/ForbiddenPanel";
import LoadingSpinner from "../common/LoadingSpinner";
import NotFoundPanel from "../common/NotFoundPanel";
import { ALL_DIETARY_TAGS } from "../../utils/dietaryTags";
import { fromDateTimeLocalInputValue, toDateTimeLocalInputValue } from "../../utils/dateTimeLocal";

// Mirrors Backend/NetromEcoMeal/Constants/ImageUpload.cs — kept here rather than a shared constant
// since this is currently the only React page with a photo-upload field.
const MAX_IMAGE_BYTES = 5 * 1024 * 1024;
const ALLOWED_IMAGE_EXTENSIONS = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

function pad2(n: number): string {
  return String(n).padStart(2, "0");
}

interface FieldErrors {
  name?: string;
  description?: string;
  price?: string;
  quantity?: string;
  weightKg?: string;
  pickupEnd?: string;
}

// Ports PackageForm.razor (/packages/create and /packages/edit/:id). A BusinessManager is offered
// every business they're staffed at (not just the one currently selected in the sidebar switcher,
// per the Razor page's own comment), but can't change the selection — the dropdown is disabled and
// defaults to whichever business is currently selected.
function PackageForm() {
  const { id } = useParams<{ id?: string }>();
  const isEdit = id !== undefined;
  const navigate = useNavigate();
  const { user } = useAuth();
  const { myBusinesses, selectedBusinessId, loading: managedBusinessLoading } = useManagedBusiness();
  const isAdmin = user?.role === "Admin";

  const [loading, setLoading] = useState(true);
  const [forbidden, setForbidden] = useState(false);
  const [forbiddenReason, setForbiddenReason] = useState("");
  const [notFound, setNotFound] = useState(false);

  const [packageTypes, setPackageTypes] = useState<PackageTypeDto[]>([]);
  const [businesses, setBusinesses] = useState<BusinessDto[]>([]);

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [businessId, setBusinessId] = useState("");
  const [packageTypeId, setPackageTypeId] = useState("");
  const [price, setPrice] = useState(0);
  const [quantity, setQuantity] = useState(1);
  const [weightKg, setWeightKg] = useState(1);
  const [dietaryTags, setDietaryTags] = useState<string[]>([]);
  const [pickupStart, setPickupStart] = useState("");
  const [pickupEnd, setPickupEnd] = useState("");
  const [imageUrl, setImageUrl] = useState("");
  const [repeatDaily, setRepeatDaily] = useState(false);

  const [errors, setErrors] = useState<FieldErrors>({});
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const [uploadingImage, setUploadingImage] = useState(false);
  const [imageUploadError, setImageUploadError] = useState<string | null>(null);
  const [draftingDescription, setDraftingDescription] = useState(false);
  const [aiDraftError, setAiDraftError] = useState<string | null>(null);

  useEffect(() => {
    // Wait for the manager's staffed-business list to resolve before deciding forbidden/defaults.
    if (!isAdmin && managedBusinessLoading) return;
    let cancelled = false;

    async function load() {
      if (!isAdmin && myBusinesses.length === 0) {
        if (!cancelled) {
          setForbidden(true);
          setForbiddenReason("You don't manage a business yet, so you can't add or edit packages.");
          setLoading(false);
        }
        return;
      }

      // Clears a forbidden/not-found verdict from an earlier run now that this run has a real
      // business list to check against (e.g. ManagedBusinessContext resolving after this effect's
      // own first, premature pass — see ManagedBusinessProvider's own comment on why `loading` is
      // derived rather than a separate flag this effect could race against).
      setForbidden(false);
      setNotFound(false);

      const [types, businessList] = await Promise.all([packageTypesApi.getAll(), isAdmin ? businessesApi.getAll() : Promise.resolve(myBusinesses)]);
      if (cancelled) return;
      setPackageTypes(types);
      setBusinesses(businessList);

      if (isEdit && id) {
        let existing;
        try {
          existing = await packagesApi.getById(id);
        } catch (err) {
          if (!cancelled) {
            if (err instanceof ApiError && err.status === 404) setNotFound(true);
            else {
              setForbidden(true);
              setForbiddenReason("You can only edit packages that belong to your business.");
            }
            setLoading(false);
          }
          return;
        }
        if (cancelled) return;

        // Checked against every business this staffer belongs to (not just the one currently
        // selected in the switcher) so they can still edit a package elsewhere they're staff.
        if (!isAdmin && !myBusinesses.some((b) => b.id === existing.businessId)) {
          setForbidden(true);
          setForbiddenReason("You can only edit packages that belong to your business.");
          setLoading(false);
          return;
        }

        setName(existing.name);
        setDescription(existing.description);
        setPrice(existing.price);
        setQuantity(existing.quantity);
        setWeightKg(existing.weightKg);
        setDietaryTags([...existing.dietaryTags]);
        setPickupStart(toDateTimeLocalInputValue(existing.pickupStart));
        setPickupEnd(toDateTimeLocalInputValue(existing.pickupEnd));
        setImageUrl(existing.imageUrl ?? "");
        setBusinessId(existing.businessId);
        setPackageTypeId(existing.packageTypeId);
      } else {
        if (types.length > 0) setPackageTypeId(types[0].id);
        setBusinessId(!isAdmin && selectedBusinessId ? selectedBusinessId : (businessList[0]?.id ?? ""));

        const now = new Date();
        const later = new Date(now.getTime() + 2 * 60 * 60 * 1000);
        setPickupStart(toDateTimeLocalInputValue(now.toISOString()));
        setPickupEnd(toDateTimeLocalInputValue(later.toISOString()));
      }

      setLoading(false);
    }

    // Genuine data-fetch effect — loads lookups and (when editing) the existing package before the
    // form can render; every setState above resolves after one of these awaits.
    // oxlint-disable-next-line react/set-state-in-effect
    load();
    return () => {
      cancelled = true;
    };
  }, [isAdmin, managedBusinessLoading, myBusinesses, isEdit, id, selectedBusinessId]);

  function toggleTag(tag: string, selected: boolean) {
    setDietaryTags((prev) => (selected ? (prev.includes(tag) ? prev : [...prev, tag]) : prev.filter((t) => t !== tag)));
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
    const extension = `.${file.name.split(".").pop()?.toLowerCase() ?? ""}`;
    if (!ALLOWED_IMAGE_EXTENSIONS.includes(extension)) {
      setImageUploadError(`"${extension}" isn't a supported image type — use JPG, PNG, WEBP, or GIF.`);
      return;
    }

    setUploadingImage(true);
    try {
      const url = await uploadsApi.upload("packages", file);
      setImageUrl(url);
    } catch (err) {
      setImageUploadError(err instanceof ApiError ? err.message : "Couldn't upload this image. Please try again.");
    } finally {
      setUploadingImage(false);
    }
  }

  async function draftDescription() {
    setAiDraftError(null);
    setDraftingDescription(true);
    try {
      const packageTypeName = packageTypes.find((t) => t.id === packageTypeId)?.name ?? "";
      const draft = await aiApi.draftDescription(name, packageTypeName, dietaryTags);
      setDescription(draft);
    } catch (err) {
      setAiDraftError(err instanceof ApiError && err.status === 409 ? err.message : "AI isn't available right now.");
    } finally {
      setDraftingDescription(false);
    }
  }

  function validate(): FieldErrors {
    const next: FieldErrors = {};
    if (!name.trim()) next.name = "Enter a package name.";
    else if (name.length > 120) next.name = "Name can't be longer than 120 characters.";

    if (!description.trim()) next.description = "Enter a short description.";
    else if (description.length > 2000) next.description = "Description can't be longer than 2,000 characters.";

    if (Number.isNaN(price) || price < 0.01 || price > 10000) next.price = "Price must be between 0.01 and 10,000.";

    if (!Number.isInteger(quantity) || quantity < 1 || quantity > 1000) next.quantity = "Quantity must be between 1 and 1,000.";

    if (Number.isNaN(weightKg) || weightKg < 0.01 || weightKg > 100) next.weightKg = "Weight must be between 0.01 and 100.";

    if (pickupStart && pickupEnd && new Date(pickupEnd).getTime() <= new Date(pickupStart).getTime()) {
      next.pickupEnd = "Pickup end must be after pickup start.";
      // Only pickupEnd-vs-now is checked (not pickupStart-vs-now) so editing a package that's
      // already mid-window doesn't get blocked — same as the Blazor model's IValidatableObject.
    } else if (pickupEnd && new Date(pickupEnd).getTime() <= Date.now()) {
      next.pickupEnd = "Pickup end must be in the future.";
    }

    return next;
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const fieldErrors = validate();
    setErrors(fieldErrors);
    if (Object.keys(fieldErrors).length > 0) return;

    setSubmitError(null);
    setSubmitting(true);

    // The form works entirely in the viewer's local time — convert back to UTC for storage.
    const pickupStartUtc = fromDateTimeLocalInputValue(pickupStart);
    const pickupEndUtc = fromDateTimeLocalInputValue(pickupEnd);

    const payload: PackageWriteRequest = {
      businessId,
      packageTypeId,
      name: name.trim(),
      description: description.trim(),
      price,
      quantity,
      weightKg,
      dietaryTags,
      pickupStart: pickupStartUtc,
      pickupEnd: pickupEndUtc,
      imageUrl: imageUrl || null,
    };

    try {
      if (isEdit && id) {
        await packagesApi.update(id, payload);
      } else {
        const created = await packagesApi.create(payload);
        if (repeatDaily) {
          const start = new Date(pickupStartUtc);
          const end = new Date(pickupEndUtc);
          const timeOfDay = (d: Date) => `${pad2(d.getUTCHours())}:${pad2(d.getUTCMinutes())}:${pad2(d.getUTCSeconds())}`;
          await packageTemplatesApi.create(created.id, timeOfDay(start), timeOfDay(end));
        }
      }
      navigate("/packages");
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.message : "Couldn't save this package. Please try again.");
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) return <LoadingSpinner />;
  if (forbidden) return <ForbiddenPanel message={forbiddenReason} backHref="/packages" backLabel="Back to packages" />;
  if (notFound) {
    return <NotFoundPanel title="Package not found" message="This package no longer exists — it may have already been deleted." backHref="/packages" backLabel="Back to packages" />;
  }

  return (
    <div>
      <div className="d-flex justify-content-between align-items-start mb-4">
        <div>
          <h1 className="h3 fw-bold mb-1">{isEdit ? "Edit Package" : "Create Package"}</h1>
          <p className="text-muted small mb-0">{isEdit ? "Update the package details" : "Add a new food package to the platform"}</p>
        </div>
        <Link to="/packages" className="btn btn-outline-secondary">
          <i className="bi bi-arrow-left me-1" /> Back
        </Link>
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
                  <input
                    id="name"
                    className="form-control"
                    placeholder="e.g. Morning Pastry Box"
                    maxLength={120}
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                  />
                  {errors.name && <div className="validation-message small d-block mt-1">{errors.name}</div>}
                </div>

                <div className="mb-3">
                  <div className="d-flex justify-content-between align-items-center">
                    <label htmlFor="description" className="form-label fw-semibold mb-0">
                      Description
                    </label>
                    <button type="button" className="btn btn-sm btn-outline-secondary" disabled={draftingDescription || !name.trim()} onClick={draftDescription}>
                      {draftingDescription ? <span className="spinner-border spinner-border-sm me-1" role="status" /> : <i className="bi bi-magic me-1" />}
                      Write it for me
                    </button>
                  </div>
                  <input
                    id="description"
                    className="form-control mt-1"
                    placeholder="Short description of the package"
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                  />
                  {errors.description && <div className="validation-message small d-block mt-1">{errors.description}</div>}
                  {aiDraftError && <div className="text-danger small mt-1">{aiDraftError}</div>}
                </div>

                <div className="row g-3 mb-3">
                  <div className="col-sm-6">
                    <label htmlFor="business" className="form-label fw-semibold">
                      Business
                    </label>
                    <select id="business" className="form-select" value={businessId} disabled={!isAdmin} onChange={(e) => setBusinessId(e.target.value)}>
                      {businesses.map((business) => (
                        <option value={business.id} key={business.id}>
                          {business.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="col-sm-6">
                    <label htmlFor="packageType" className="form-label fw-semibold">
                      Type
                    </label>
                    <select id="packageType" className="form-select" value={packageTypeId} onChange={(e) => setPackageTypeId(e.target.value)}>
                      {packageTypes.map((type) => (
                        <option value={type.id} key={type.id}>
                          {type.name}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>

                <div className="row g-3 mb-3">
                  <div className="col-sm-6">
                    <label htmlFor="price" className="form-label fw-semibold">
                      Price
                    </label>
                    <div className="input-group">
                      <span className="input-group-text">RON</span>
                      <input
                        id="price"
                        type="number"
                        step="0.01"
                        className="form-control"
                        placeholder="0.00"
                        value={price}
                        onChange={(e) => setPrice(e.target.valueAsNumber)}
                      />
                    </div>
                    {errors.price && <div className="validation-message small d-block mt-1">{errors.price}</div>}
                  </div>
                  <div className="col-sm-6">
                    <label htmlFor="quantity" className="form-label fw-semibold">
                      Quantity
                    </label>
                    <input id="quantity" type="number" className="form-control" placeholder="1" value={quantity} onChange={(e) => setQuantity(e.target.valueAsNumber)} />
                    {errors.quantity && <div className="validation-message small d-block mt-1">{errors.quantity}</div>}
                  </div>
                </div>

                <div className="row g-3 mb-3">
                  <div className="col-sm-6">
                    <label htmlFor="weightKg" className="form-label fw-semibold">
                      Weight (kg)
                    </label>
                    <input
                      id="weightKg"
                      type="number"
                      step="0.01"
                      className="form-control"
                      placeholder="1.0"
                      value={weightKg}
                      onChange={(e) => setWeightKg(e.target.valueAsNumber)}
                    />
                    {errors.weightKg && <div className="validation-message small d-block mt-1">{errors.weightKg}</div>}
                  </div>
                </div>

                <div className="mb-3">
                  <label className="form-label fw-semibold">
                    Dietary &amp; allergen tags <span className="text-muted fw-normal">(optional)</span>
                  </label>
                  <div className="pkg-form-tag-grid">
                    {ALL_DIETARY_TAGS.map((tag) => (
                      <label className="pkg-form-tag-check" key={tag}>
                        <input type="checkbox" checked={dietaryTags.includes(tag)} onChange={(e) => toggleTag(tag, e.target.checked)} />
                        {tag}
                      </label>
                    ))}
                  </div>
                </div>

                <div className="row g-3 mb-3">
                  <div className="col-sm-6">
                    <label htmlFor="pickupStart" className="form-label fw-semibold">
                      Pickup Start
                    </label>
                    <input id="pickupStart" type="datetime-local" className="form-control" value={pickupStart} onChange={(e) => setPickupStart(e.target.value)} />
                  </div>
                  <div className="col-sm-6">
                    <label htmlFor="pickupEnd" className="form-label fw-semibold">
                      Pickup End
                    </label>
                    <input id="pickupEnd" type="datetime-local" className="form-control" value={pickupEnd} onChange={(e) => setPickupEnd(e.target.value)} />
                  </div>
                  {errors.pickupEnd && <div className="validation-message small d-block mt-1">{errors.pickupEnd}</div>}
                </div>

                <div className="mb-4">
                  <label htmlFor="imageFile" className="form-label fw-semibold">
                    Photo <span className="text-muted fw-normal">(optional)</span>
                  </label>
                  <div className="d-flex align-items-start gap-3">
                    {imageUrl.trim() !== "" && <img src={imageUrl} alt="Package photo preview" className="form-image-preview" />}
                    <div className="flex-grow-1">
                      <input id="imageFile" type="file" className="form-control" accept="image/*" disabled={uploadingImage} onChange={onImageSelected} />
                      <div className="form-text">Or paste an image URL directly:</div>
                      <input
                        className="form-control form-control-sm mt-1"
                        placeholder="https://..."
                        value={imageUrl}
                        onChange={(e) => setImageUrl(e.target.value)}
                      />
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

                {!isEdit && (
                  <div className="mb-4 form-check">
                    <input type="checkbox" className="form-check-input" id="repeatDaily" checked={repeatDaily} onChange={(e) => setRepeatDaily(e.target.checked)} />
                    <label className="form-check-label" htmlFor="repeatDaily">
                      <i className="bi bi-arrow-repeat me-1" />
                      Repeat this every day
                    </label>
                    <div className="form-text">
                      A fresh package with this same name, price, and pickup window ({pickupStart.slice(11, 16)}–{pickupEnd.slice(11, 16)}) is created automatically each
                      day.
                    </div>
                  </div>
                )}

                {submitError && (
                  <div className="alert alert-danger py-2 small" role="alert">
                    {submitError}
                  </div>
                )}

                <button type="submit" className="btn btn-primary w-100 py-2" disabled={submitting}>
                  {submitting && <span className="spinner-border spinner-border-sm me-2" role="status" />}
                  <i className="bi bi-check-lg me-1" /> {isEdit ? "Save Changes" : "Create Package"}
                </button>
              </form>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default PackageForm;
