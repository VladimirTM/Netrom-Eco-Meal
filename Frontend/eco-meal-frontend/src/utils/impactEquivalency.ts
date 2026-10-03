// Mirrors Constants.ImpactEquivalency — round, published planet-scale averages, not a precise LCA.
const CO2E_KG_PER_KG_FOOD = 2.5;
const WATER_LITERS_PER_KG_FOOD = 1000;
const CAR_CO2E_KG_PER_KM = 0.19;

export function co2eKg(kgSaved: number): number {
  return kgSaved * CO2E_KG_PER_KG_FOOD;
}

export function litersOfWater(kgSaved: number): number {
  return kgSaved * WATER_LITERS_PER_KG_FOOD;
}

export function kmNotDriven(kgSaved: number): number {
  return co2eKg(kgSaved) / CAR_CO2E_KG_PER_KM;
}
