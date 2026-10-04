import { useEffect, useState, type KeyboardEvent } from "react";
import { Link } from "react-router-dom";
import { businessesApi } from "../../api/clients/BusinessesApiClient";
import { businessTypesApi } from "../../api/clients/BusinessTypesApiClient";
import { brandsApi } from "../../api/clients/BrandsApiClient";
import { packageTypesApi } from "../../api/clients/PackageTypesApiClient";
import type { BrandDto, BusinessTypeDto, PackageTypeDto } from "../../api/models/Lookup";
import ConfirmDialog from "../common/ConfirmDialog";

type DeleteKind = "business" | "package" | "brand";
type PendingDelete = { kind: DeleteKind; id: string; name: string };

function errorMessage(err: unknown, fallback: string): string {
  return err instanceof Error ? err.message : fallback;
}

// Enter-to-submit for the three "add" inputs.
function handleNewTypeKeyUp(e: KeyboardEvent<HTMLInputElement>, submit: () => void) {
  if (e.key === "Enter") submit();
}

// Ports Types.razor (/types): three independent cards — kitchen types, package types, brands —
// each with identical inline add/rename/delete UX, sharing one ConfirmDialog for delete.
function Types() {
  const [businessTypes, setBusinessTypes] = useState<BusinessTypeDto[] | null>(null);
  const [packageTypes, setPackageTypes] = useState<PackageTypeDto[] | null>(null);
  const [brands, setBrands] = useState<BrandDto[] | null>(null);
  const [brandBusinessCounts, setBrandBusinessCounts] = useState<Record<string, number>>({});

  const [newBusinessTypeName, setNewBusinessTypeName] = useState("");
  const [newPackageTypeName, setNewPackageTypeName] = useState("");
  const [newBrandName, setNewBrandName] = useState("");
  const [addingBusinessType, setAddingBusinessType] = useState(false);
  const [addingPackageType, setAddingPackageType] = useState(false);
  const [addingBrand, setAddingBrand] = useState(false);
  const [businessTypeError, setBusinessTypeError] = useState<string | null>(null);
  const [packageTypeError, setPackageTypeError] = useState<string | null>(null);
  const [brandError, setBrandError] = useState<string | null>(null);

  const [editingBusinessTypeId, setEditingBusinessTypeId] = useState<string | null>(null);
  const [editingBusinessTypeName, setEditingBusinessTypeName] = useState("");
  const [editingPackageTypeId, setEditingPackageTypeId] = useState<string | null>(null);
  const [editingPackageTypeName, setEditingPackageTypeName] = useState("");
  const [editingBrandId, setEditingBrandId] = useState<string | null>(null);
  const [editingBrandName, setEditingBrandName] = useState("");

  const [pendingDelete, setPendingDelete] = useState<PendingDelete | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);

  async function reloadBusinessTypes() {
    setBusinessTypes(await businessTypesApi.getAll());
  }
  async function reloadPackageTypes() {
    setPackageTypes(await packageTypesApi.getAll());
  }
  async function reloadBrands() {
    const [brandList, allBusinesses] = await Promise.all([brandsApi.getAll(), businessesApi.getAll()]);
    setBrands(brandList);
    const counts: Record<string, number> = {};
    for (const b of allBusinesses) {
      if (b.brandId) counts[b.brandId] = (counts[b.brandId] ?? 0) + 1;
    }
    setBrandBusinessCounts(counts);
  }

  useEffect(() => {
    // oxlint-disable-next-line react/set-state-in-effect
    reloadBusinessTypes();
    // oxlint-disable-next-line react/set-state-in-effect
    reloadPackageTypes();
    // oxlint-disable-next-line react/set-state-in-effect
    reloadBrands();
  }, []);

  async function addBusinessType() {
    setBusinessTypeError(null);
    if (!newBusinessTypeName.trim()) return;
    setAddingBusinessType(true);
    try {
      await businessTypesApi.add(newBusinessTypeName.trim());
      setNewBusinessTypeName("");
      await reloadBusinessTypes();
    } catch (err) {
      setBusinessTypeError(errorMessage(err, "That name is already taken."));
    } finally {
      setAddingBusinessType(false);
    }
  }

  function startEditBusinessType(type: BusinessTypeDto) {
    setEditingBusinessTypeId(type.id);
    setEditingBusinessTypeName(type.name);
  }

  async function saveBusinessType(type: BusinessTypeDto) {
    setBusinessTypeError(null);
    if (!editingBusinessTypeName.trim()) return;
    try {
      await businessTypesApi.update(type.id, editingBusinessTypeName.trim());
      setEditingBusinessTypeId(null);
      await reloadBusinessTypes();
    } catch (err) {
      setBusinessTypeError(errorMessage(err, "That name is already taken."));
    }
  }

  async function addPackageType() {
    setPackageTypeError(null);
    if (!newPackageTypeName.trim()) return;
    setAddingPackageType(true);
    try {
      await packageTypesApi.add(newPackageTypeName.trim());
      setNewPackageTypeName("");
      await reloadPackageTypes();
    } catch (err) {
      setPackageTypeError(errorMessage(err, "That name is already taken."));
    } finally {
      setAddingPackageType(false);
    }
  }

  function startEditPackageType(type: PackageTypeDto) {
    setEditingPackageTypeId(type.id);
    setEditingPackageTypeName(type.name);
  }

  async function savePackageType(type: PackageTypeDto) {
    setPackageTypeError(null);
    if (!editingPackageTypeName.trim()) return;
    try {
      await packageTypesApi.update(type.id, editingPackageTypeName.trim());
      setEditingPackageTypeId(null);
      await reloadPackageTypes();
    } catch (err) {
      setPackageTypeError(errorMessage(err, "That name is already taken."));
    }
  }

  async function addBrand() {
    setBrandError(null);
    if (!newBrandName.trim()) return;
    setAddingBrand(true);
    try {
      await brandsApi.add(newBrandName.trim());
      setNewBrandName("");
      await reloadBrands();
    } catch (err) {
      setBrandError(errorMessage(err, "That name is already taken."));
    } finally {
      setAddingBrand(false);
    }
  }

  function startEditBrand(brand: BrandDto) {
    setEditingBrandId(brand.id);
    setEditingBrandName(brand.name);
  }

  async function saveBrand(brand: BrandDto) {
    setBrandError(null);
    if (!editingBrandName.trim()) return;
    try {
      await brandsApi.update(brand.id, editingBrandName.trim(), brand.description);
      setEditingBrandId(null);
      await reloadBrands();
    } catch (err) {
      setBrandError(errorMessage(err, "That name is already taken."));
    }
  }

  async function confirmDelete() {
    const pending = pendingDelete;
    if (!pending) return;

    setDeleteBusy(true);
    try {
      if (pending.kind === "business") await businessTypesApi.delete(pending.id);
      else if (pending.kind === "package") await packageTypesApi.delete(pending.id);
      else await brandsApi.delete(pending.id);
    } catch (err) {
      const message = errorMessage(err, "That's still in use.");
      if (pending.kind === "business") setBusinessTypeError(message);
      else if (pending.kind === "package") setPackageTypeError(message);
      else setBrandError(message);
      setDeleteBusy(false);
      setPendingDelete(null);
      return;
    }

    setDeleteBusy(false);
    setPendingDelete(null);
    if (pending.kind === "business") await reloadBusinessTypes();
    else if (pending.kind === "package") await reloadPackageTypes();
    else await reloadBrands();
  }

  return (
    <div>
      <div className="mb-4">
        <h1 className="h3 fw-bold mb-1">Kitchen &amp; Package Types</h1>
        <p className="text-muted small mb-0">Add, rename, or remove the categories businesses and packages pick from — no code change or migration needed for a new one.</p>
      </div>

      <div className="row g-4">
        <div className="col-lg-4">
          <div className="card border-0 shadow-sm h-100">
            <div className="card-body">
              <h2 className="h6 fw-bold mb-3">
                <i className="bi bi-shop me-2" />
                Kitchen types
              </h2>

              {businessTypeError && <div className="alert alert-danger py-2 small">{businessTypeError}</div>}

              {businessTypes === null ? (
                <div className="d-flex justify-content-center py-4">
                  <div className="spinner-border spinner-border-sm text-primary" role="status">
                    <span className="visually-hidden">Loading...</span>
                  </div>
                </div>
              ) : businessTypes.length === 0 ? (
                <p className="text-muted small">No kitchen types yet.</p>
              ) : (
                <ul className="list-group list-group-flush mb-3">
                  {businessTypes.map((type) => (
                    <li className="list-group-item d-flex justify-content-between align-items-center px-0" key={type.id}>
                      {editingBusinessTypeId === type.id ? (
                        <>
                          <input type="text" className="form-control form-control-sm me-2" value={editingBusinessTypeName} onChange={(e) => setEditingBusinessTypeName(e.target.value)} />
                          <div className="d-flex gap-1">
                            <button type="button" className="btn btn-sm btn-outline-primary" title="Save" onClick={() => saveBusinessType(type)}>
                              <i className="bi bi-check-lg" />
                            </button>
                            <button type="button" className="btn btn-sm btn-outline-secondary" title="Cancel" onClick={() => setEditingBusinessTypeId(null)}>
                              <i className="bi bi-x-lg" />
                            </button>
                          </div>
                        </>
                      ) : (
                        <>
                          <span>{type.name}</span>
                          <div className="d-flex gap-1">
                            <button type="button" className="btn btn-sm btn-outline-secondary" title="Rename" onClick={() => startEditBusinessType(type)}>
                              <i className="bi bi-pencil" />
                            </button>
                            <button type="button" className="btn btn-sm btn-outline-danger" title="Delete" onClick={() => setPendingDelete({ kind: "business", id: type.id, name: type.name })}>
                              <i className="bi bi-trash" />
                            </button>
                          </div>
                        </>
                      )}
                    </li>
                  ))}
                </ul>
              )}

              <div className="input-group input-group-sm">
                <input
                  type="text"
                  className="form-control"
                  placeholder="e.g. Food Truck"
                  value={newBusinessTypeName}
                  onChange={(e) => setNewBusinessTypeName(e.target.value)}
                  onKeyUp={(e) => handleNewTypeKeyUp(e, addBusinessType)}
                />
                <button type="button" className="btn btn-outline-primary" title="Add kitchen type" disabled={addingBusinessType} onClick={addBusinessType}>
                  {addingBusinessType ? <span className="spinner-border spinner-border-sm" role="status" /> : <i className="bi bi-plus-lg" />}
                </button>
              </div>
            </div>
          </div>
        </div>

        <div className="col-lg-4">
          <div className="card border-0 shadow-sm h-100">
            <div className="card-body">
              <h2 className="h6 fw-bold mb-3">
                <i className="bi bi-box-seam me-2" />
                Package types
              </h2>

              {packageTypeError && <div className="alert alert-danger py-2 small">{packageTypeError}</div>}

              {packageTypes === null ? (
                <div className="d-flex justify-content-center py-4">
                  <div className="spinner-border spinner-border-sm text-primary" role="status">
                    <span className="visually-hidden">Loading...</span>
                  </div>
                </div>
              ) : packageTypes.length === 0 ? (
                <p className="text-muted small">No package types yet.</p>
              ) : (
                <ul className="list-group list-group-flush mb-3">
                  {packageTypes.map((type) => (
                    <li className="list-group-item d-flex justify-content-between align-items-center px-0" key={type.id}>
                      {editingPackageTypeId === type.id ? (
                        <>
                          <input type="text" className="form-control form-control-sm me-2" value={editingPackageTypeName} onChange={(e) => setEditingPackageTypeName(e.target.value)} />
                          <div className="d-flex gap-1">
                            <button type="button" className="btn btn-sm btn-outline-primary" title="Save" onClick={() => savePackageType(type)}>
                              <i className="bi bi-check-lg" />
                            </button>
                            <button type="button" className="btn btn-sm btn-outline-secondary" title="Cancel" onClick={() => setEditingPackageTypeId(null)}>
                              <i className="bi bi-x-lg" />
                            </button>
                          </div>
                        </>
                      ) : (
                        <>
                          <span>{type.name}</span>
                          <div className="d-flex gap-1">
                            <button type="button" className="btn btn-sm btn-outline-secondary" title="Rename" onClick={() => startEditPackageType(type)}>
                              <i className="bi bi-pencil" />
                            </button>
                            <button type="button" className="btn btn-sm btn-outline-danger" title="Delete" onClick={() => setPendingDelete({ kind: "package", id: type.id, name: type.name })}>
                              <i className="bi bi-trash" />
                            </button>
                          </div>
                        </>
                      )}
                    </li>
                  ))}
                </ul>
              )}

              <div className="input-group input-group-sm">
                <input
                  type="text"
                  className="form-control"
                  placeholder="e.g. Salad Box"
                  value={newPackageTypeName}
                  onChange={(e) => setNewPackageTypeName(e.target.value)}
                  onKeyUp={(e) => handleNewTypeKeyUp(e, addPackageType)}
                />
                <button type="button" className="btn btn-outline-primary" title="Add package type" disabled={addingPackageType} onClick={addPackageType}>
                  {addingPackageType ? <span className="spinner-border spinner-border-sm" role="status" /> : <i className="bi bi-plus-lg" />}
                </button>
              </div>
            </div>
          </div>
        </div>

        <div className="col-lg-4">
          <div className="card border-0 shadow-sm h-100">
            <div className="card-body">
              <h2 className="h6 fw-bold mb-3">
                <i className="bi bi-diagram-3 me-2" />
                Brands
              </h2>
              <p className="text-muted small">Groups several locations (e.g. three branches of the same bakery) under one shared public page — see the "Brand" field on a business's edit form.</p>

              {brandError && <div className="alert alert-danger py-2 small">{brandError}</div>}

              {brands === null ? (
                <div className="d-flex justify-content-center py-4">
                  <div className="spinner-border spinner-border-sm text-primary" role="status">
                    <span className="visually-hidden">Loading...</span>
                  </div>
                </div>
              ) : brands.length === 0 ? (
                <p className="text-muted small">No brands yet.</p>
              ) : (
                <ul className="list-group list-group-flush mb-3">
                  {brands.map((brand) => (
                    <li className="list-group-item d-flex justify-content-between align-items-center px-0" key={brand.id}>
                      {editingBrandId === brand.id ? (
                        <>
                          <input type="text" className="form-control form-control-sm me-2" value={editingBrandName} onChange={(e) => setEditingBrandName(e.target.value)} />
                          <div className="d-flex gap-1">
                            <button type="button" className="btn btn-sm btn-outline-primary" title="Save" onClick={() => saveBrand(brand)}>
                              <i className="bi bi-check-lg" />
                            </button>
                            <button type="button" className="btn btn-sm btn-outline-secondary" title="Cancel" onClick={() => setEditingBrandId(null)}>
                              <i className="bi bi-x-lg" />
                            </button>
                          </div>
                        </>
                      ) : (
                        <>
                          <Link to={`/brands/${brand.id}`} className="text-decoration-none">
                            {brand.name} <span className="text-muted small">({brandBusinessCounts[brand.id] ?? 0})</span>
                          </Link>
                          <div className="d-flex gap-1">
                            <button type="button" className="btn btn-sm btn-outline-secondary" title="Rename" onClick={() => startEditBrand(brand)}>
                              <i className="bi bi-pencil" />
                            </button>
                            <button type="button" className="btn btn-sm btn-outline-danger" title="Delete" onClick={() => setPendingDelete({ kind: "brand", id: brand.id, name: brand.name })}>
                              <i className="bi bi-trash" />
                            </button>
                          </div>
                        </>
                      )}
                    </li>
                  ))}
                </ul>
              )}

              <div className="input-group input-group-sm">
                <input
                  type="text"
                  className="form-control"
                  placeholder="e.g. Hat-Trick Bakeries"
                  value={newBrandName}
                  onChange={(e) => setNewBrandName(e.target.value)}
                  onKeyUp={(e) => handleNewTypeKeyUp(e, addBrand)}
                />
                <button type="button" className="btn btn-outline-primary" title="Add brand" disabled={addingBrand} onClick={addBrand}>
                  {addingBrand ? <span className="spinner-border spinner-border-sm" role="status" /> : <i className="bi bi-plus-lg" />}
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <ConfirmDialog
        isOpen={pendingDelete !== null}
        title={`Delete "${pendingDelete?.name}"?`}
        message="This can't be undone. It's blocked if anything still depends on it."
        confirmLabel="Delete"
        busy={deleteBusy}
        onConfirm={confirmDelete}
        onCancel={() => setPendingDelete(null)}
      />
    </div>
  );
}

export default Types;
