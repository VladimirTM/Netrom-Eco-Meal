// Mirrors Constants.DietaryTags — free-form labels a manager can attach to a Package.
export const DietaryTags = {
  Vegetarian: "Vegetarian",
  Vegan: "Vegan",
  GlutenFree: "Gluten-Free",
  DairyFree: "Dairy-Free",
  Halal: "Halal",
  ContainsNuts: "Contains Nuts",
  ContainsGluten: "Contains Gluten",
  ContainsDairy: "Contains Dairy",
} as const;

export const ALL_DIETARY_TAGS = [
  DietaryTags.Vegetarian,
  DietaryTags.Vegan,
  DietaryTags.GlutenFree,
  DietaryTags.DairyFree,
  DietaryTags.Halal,
  DietaryTags.ContainsNuts,
  DietaryTags.ContainsGluten,
  DietaryTags.ContainsDairy,
];

export const ALLERGEN_TAGS = [DietaryTags.ContainsNuts, DietaryTags.ContainsGluten, DietaryTags.ContainsDairy];

export function isAllergen(tag: string): boolean {
  return (ALLERGEN_TAGS as string[]).includes(tag);
}
