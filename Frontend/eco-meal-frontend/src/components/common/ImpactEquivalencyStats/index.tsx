import { kmNotDriven, litersOfWater } from "../../../utils/impactEquivalency";

interface ImpactEquivalencyStatsProps {
  kgSaved: number;
}

// Ports ImpactEquivalencyStats.razor — turns a raw kg-saved figure into km-not-driven/liters-of-water
// via utils/impactEquivalency, so the order receipt (OrderDetailModal) and /impact always agree.
function ImpactEquivalencyStats({ kgSaved }: ImpactEquivalencyStatsProps) {
  if (kgSaved <= 0) return null;

  return (
    <div className="impact-equivalency">
      <span className="impact-equivalency-item">
        <i className="bi bi-car-front" /> ~{kmNotDriven(kgSaved).toFixed(0)} km not driven
      </span>
      <span className="impact-equivalency-item">
        <i className="bi bi-droplet" /> ~{Math.round(litersOfWater(kgSaved)).toLocaleString()} L of water saved
      </span>
    </div>
  );
}

export default ImpactEquivalencyStats;
