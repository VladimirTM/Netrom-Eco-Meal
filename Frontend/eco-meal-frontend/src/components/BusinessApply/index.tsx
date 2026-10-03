import { useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../../api/base/http";
import { businessTypesApi } from "../../api/clients/BusinessTypesApiClient";
import { businessesApi } from "../../api/clients/BusinessesApiClient";
import type { BusinessTypeDto } from "../../api/models/Lookup";

// Ports BusinessApply.razor (/businesses/apply) — gated to Customer/BusinessManager (matches the
// Blazor page's own [Authorize(Roles = "Customer,BusinessManager")] exactly).
function BusinessApply() {
  const [businessTypes, setBusinessTypes] = useState<BusinessTypeDto[]>([]);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [address, setAddress] = useState("");
  const [businessTypeId, setBusinessTypeId] = useState("");
  const [imageUrl, setImageUrl] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [submittedName, setSubmittedName] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      const types = await businessTypesApi.getAll();
      setBusinessTypes(types);
      if (types.length > 0) setBusinessTypeId(types[0].id);
    })();
  }, []);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (!name.trim() || !description.trim() || !address.trim() || !businessTypeId) return;

    setSubmitting(true);
    setError(null);
    try {
      await businessesApi.apply({
        name,
        description,
        address,
        imageUrl: imageUrl || null,
        latitude: null,
        longitude: null,
        businessTypeId,
        brandId: null,
        loyaltyPunchThreshold: null,
        loyaltyDiscountAmount: null,
      });
      setSubmittedName(name);
      setSubmitted(true);
    } catch (err) {
      setError(err instanceof ApiError && err.status === 401 ? "You must be signed in to submit an application." : "Couldn't submit your application. Please try again.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="container py-4">
      <div className="text-center mb-4">
        <h1 className="h3 fw-bold mb-1">List your business</h1>
        <p className="text-muted mb-0">Tell us about your kitchen — an admin will review it before it goes live.</p>
      </div>

      <div className="row justify-content-center">
        <div className="col-lg-7 col-xl-6">
          <div className="card border-0 shadow-sm">
            <div className="card-body p-4">
              {submitted ? (
                <div className="text-center py-4">
                  <i className="bi bi-check-circle-fill text-success fs-1 d-block mb-3" />
                  <h2 className="h5 fw-bold mb-2">Application submitted!</h2>
                  <p className="text-muted mb-4">An admin will review &quot;{submittedName}&quot; and let you know once it&apos;s approved.</p>
                  <Link to="/" className="btn btn-outline-primary">
                    Back to home
                  </Link>
                </div>
              ) : businessTypes.length === 0 ? (
                <div className="d-flex justify-content-center py-5">
                  <div className="spinner-border text-primary" role="status">
                    <span className="visually-hidden">Loading...</span>
                  </div>
                </div>
              ) : (
                <form onSubmit={submit}>
                  <div className="mb-3">
                    <label htmlFor="name" className="form-label fw-semibold">
                      Business name
                    </label>
                    <input id="name" className="form-control" placeholder="e.g. Green Bites" required value={name} onChange={(e) => setName(e.target.value)} />
                  </div>
                  <div className="mb-3">
                    <label htmlFor="description" className="form-label fw-semibold">
                      Description
                    </label>
                    <input
                      id="description"
                      className="form-control"
                      placeholder="Short description of the business"
                      required
                      value={description}
                      onChange={(e) => setDescription(e.target.value)}
                    />
                  </div>
                  <div className="row g-3 mb-3">
                    <div className="col-sm-7">
                      <label htmlFor="address" className="form-label fw-semibold">
                        Address
                      </label>
                      <input id="address" className="form-control" placeholder="Street, City" required value={address} onChange={(e) => setAddress(e.target.value)} />
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
                  <div className="mb-4">
                    <label htmlFor="imageUrl" className="form-label fw-semibold">
                      Image URL <span className="text-muted fw-normal">(optional)</span>
                    </label>
                    <input id="imageUrl" className="form-control" placeholder="https://..." value={imageUrl} onChange={(e) => setImageUrl(e.target.value)} />
                  </div>
                  {error && <div className="alert alert-danger py-2 small">{error}</div>}
                  <button type="submit" className="btn btn-primary w-100 py-2" disabled={submitting}>
                    {submitting && <span className="spinner-border spinner-border-sm me-2" role="status" />}
                    <i className="bi bi-send me-1" /> Submit for review
                  </button>
                </form>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default BusinessApply;
