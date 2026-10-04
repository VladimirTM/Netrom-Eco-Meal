import { http } from "../base/http";
import type { BasketPlanDto, SearchIntent } from "../models/Ai";

export const aiApi = {
  parseSearchIntent: (utterance: string, previousIntent: SearchIntent | null): Promise<SearchIntent> =>
    http.post<SearchIntent>("/ai/search-intent", { utterance, previousIntent }),

  proposeBasket: (peopleCount: number, budget: number, dietaryTag: string | null): Promise<BasketPlanDto> =>
    http.post<BasketPlanDto>("/ai/basket-plan", { peopleCount, budget, dietaryTag }),

  draftDescription: (name: string, packageTypeName: string, dietaryTags: string[]): Promise<string> =>
    http.post<string>("/ai/draft-description", { name, packageTypeName, dietaryTags }),
};
