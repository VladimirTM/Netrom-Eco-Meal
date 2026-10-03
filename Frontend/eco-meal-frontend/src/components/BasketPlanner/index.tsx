import { useState } from "react";
import { aiApi } from "../../api/clients/AiApiClient";
import type { BasketPlanDto } from "../../api/models/Ai";
import { ApiError } from "../../api/base/http";
import { ALLERGEN_TAGS, ALL_DIETARY_TAGS } from "../../utils/dietaryTags";
import { formatCurrency } from "../../utils/currency";

function BasketPlanner() {
  const [peopleCount, setPeopleCount] = useState(4);
  const [budget, setBudget] = useState<number | undefined>(30);
  const [dietaryTag, setDietaryTag] = useState("");

  const [planning, setPlanning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [plan, setPlan] = useState<BasketPlanDto | null>(null);
  const [approved, setApproved] = useState<Set<string>>(new Set());

  const approvedTotal = plan ? plan.items.filter((i) => approved.has(i.package.id)).reduce((sum, i) => sum + i.lineTotal, 0) : 0;

  async function planBasket() {
    if (planning || !budget || budget <= 0 || peopleCount < 1) return;

    setPlanning(true);
    setError(null);
    setPlan(null);

    try {
      const result = await aiApi.proposeBasket(peopleCount, budget, dietaryTag || null);
      setPlan(result);
      setApproved(new Set(result.items.map((i) => i.package.id)));
    } catch (err) {
      setError(
        err instanceof ApiError && err.status === 409
          ? err.message
          : "The AI basket planner isn't available right now, or is taking too long to respond — please try again in a moment.",
      );
    } finally {
      setPlanning(false);
    }
  }

  function toggleApproved(packageId: string, isApproved: boolean) {
    setApproved((prev) => {
      const next = new Set(prev);
      if (isApproved) next.add(packageId);
      else next.delete(packageId);
      return next;
    });
  }

  return (
    <>
      <section className="planner-hero">
        <div className="planner-hero-inner">
          <span className="planner-hero-eyebrow">
            <i className="bi bi-stars" /> AI rescue-basket planner
          </span>
          <h1 className="planner-hero-title">Tell us the budget. We&apos;ll find the basket.</h1>
          <p className="planner-hero-sub">
            Set a headcount, a budget, and an optional dietary need — the AI searches real, live packages and
            proposes a basket from a single kitchen, with a reason for every pick. Nothing is added to your basket
            until you approve it.
          </p>
        </div>
      </section>

      <section className="planner-form-section">
        <div className="planner-form-card">
          <div className="planner-form-row">
            <label className="planner-form-label">
              Feeding
              <input type="number" min={1} max={50} className="form-control" value={peopleCount} onChange={(e) => setPeopleCount(Number(e.target.value))} />
              <span className="planner-form-hint">people</span>
            </label>

            <label className="planner-form-label">
              Budget
              <input
                type="number"
                min={1}
                step={0.5}
                className="form-control"
                value={budget ?? ""}
                onChange={(e) => setBudget(e.target.value === "" ? undefined : Number(e.target.value))}
              />
              <span className="planner-form-hint">RON total</span>
            </label>

            <label className="planner-form-label">
              Dietary need
              <select className="form-select" value={dietaryTag} onChange={(e) => setDietaryTag(e.target.value)}>
                <option value="">Any diet/allergen</option>
                <optgroup label="Dietary preference">
                  {ALL_DIETARY_TAGS.filter((tag) => !(ALLERGEN_TAGS as string[]).includes(tag)).map((tag) => (
                    <option value={tag} key={tag}>
                      {tag}
                    </option>
                  ))}
                </optgroup>
                <optgroup label="Avoid (allergen)">
                  {ALLERGEN_TAGS.map((tag) => (
                    <option value={tag} key={tag}>
                      {tag}
                    </option>
                  ))}
                </optgroup>
              </select>
            </label>
          </div>

          {error && <div className="alert alert-danger mt-3 mb-0">{error}</div>}

          <button type="button" className="btn btn-primary planner-plan-btn" disabled={planning || peopleCount < 1 || !budget || budget <= 0} onClick={() => void planBasket()}>
            {planning ? (
              <>
                <span className="spinner-border spinner-border-sm me-2" role="status" />
                Planning your basket…
              </>
            ) : (
              <>
                <i className="bi bi-magic me-2" />
                Plan my basket
              </>
            )}
          </button>
          {planning && (
            <p className="text-muted small mt-2 mb-0">This can take a few minutes — it&apos;s searching real packages and weighing options, not stuck. No need to reload.</p>
          )}
        </div>
      </section>

      {plan && (
        <section className="planner-result-section">
          <div className="planner-result-card">
            <p className="planner-explanation">
              <i className="bi bi-lightbulb" /> {plan.explanation}
            </p>

            {plan.items.length === 0 ? (
              <div className="planner-empty">
                <i className="bi bi-basket2" />
                <p className="mb-0">Nothing fit that request right now.</p>
              </div>
            ) : (
              <>
                <div className="planner-items">
                  {plan.items.map((item) => (
                    <label className={`planner-item ${approved.has(item.package.id) ? "planner-item-approved" : ""}`} key={item.package.id}>
                      <input type="checkbox" checked={approved.has(item.package.id)} onChange={(e) => toggleApproved(item.package.id, e.target.checked)} />
                      <div className="planner-item-body">
                        <div className="planner-item-top">
                          <span className="planner-item-name">
                            {item.quantity}&times; {item.package.name}
                          </span>
                          <span className="planner-item-price">{formatCurrency(item.lineTotal)}</span>
                        </div>
                        <div className="planner-item-business">
                          <i className="bi bi-shop" />
                          {item.package.businessName}
                        </div>
                        <div className="planner-item-reason">{item.reason}</div>
                      </div>
                    </label>
                  ))}
                </div>

                <div className="planner-result-footer">
                  <div className="planner-approved-total">
                    <span>Approved total</span>
                    <span className="planner-approved-total-value">{formatCurrency(approvedTotal)}</span>
                  </div>
                  {/* "Add approved to basket" needs CartContext, which is Phase 6 scope — the planner itself (propose + approve/reject +
                      total) is fully live; only the final add-to-cart hop is deferred. */}
                  <button type="button" className="btn btn-primary" disabled title="Basket arrives in Phase 6">
                    <i className="bi bi-bag-plus me-2" />
                    Add approved to basket
                  </button>
                </div>
              </>
            )}
          </div>
        </section>
      )}
    </>
  );
}

export default BasketPlanner;
