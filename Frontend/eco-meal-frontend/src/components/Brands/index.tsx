import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { brandsApi } from "../../api/clients/BrandsApiClient";
import { businessesApi } from "../../api/clients/BusinessesApiClient";
import type { BrandDto } from "../../api/models/Lookup";
import { ListRowSkeletons } from "../common/Skeleton";

interface BrandWithLocationCount extends BrandDto {
  locationCount: number;
}

function Brands() {
  const [brands, setBrands] = useState<BrandWithLocationCount[] | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      // BrandDto carries no location count, so derive it from the public businesses list.
      const [all, publicBusinesses] = await Promise.all([brandsApi.getAll(), businessesApi.getAll(true)]);
      if (cancelled) return;

      const countsByBrand = new Map<string, number>();
      for (const business of publicBusinesses) {
        if (business.brandId) countsByBrand.set(business.brandId, (countsByBrand.get(business.brandId) ?? 0) + 1);
      }

      setBrands(
        all
          .map((brand) => ({ ...brand, locationCount: countsByBrand.get(brand.id) ?? 0 }))
          .filter((brand) => brand.locationCount > 0),
      );
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div className="container py-4">
      <div className="mb-4">
        <h1 className="h3 fw-bold mb-1">Brands</h1>
        <p className="text-muted small mb-0">Chains with more than one location on Eco Meal — favorite the brand and every branch counts toward it.</p>
      </div>

      {brands === null ? (
        <ListRowSkeletons />
      ) : brands.length === 0 ? (
        <p className="text-muted">No brands yet — an admin can group locations into one from the &quot;Kitchen &amp; Package Types&quot; page.</p>
      ) : (
        <div className="row g-3">
          {brands.map((brand) => (
            <div className="col-sm-6 col-lg-4" key={brand.id}>
              <Link to={`/brands/${brand.id}`} className="text-decoration-none">
                <div className="card border-0 shadow-sm h-100">
                  <div className="card-body">
                    <h2 className="h6 fw-bold mb-1 text-body">{brand.name}</h2>
                    {brand.description && <p className="text-muted small mb-2">{brand.description}</p>}
                    <span className="text-muted small">
                      <i className="bi bi-geo-alt me-1" />
                      {brand.locationCount} location{brand.locationCount === 1 ? "" : "s"}
                    </span>
                  </div>
                </div>
              </Link>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default Brands;
