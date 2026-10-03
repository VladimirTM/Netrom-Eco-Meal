import { http } from "../base/http";

export const streaksApi = {
  getMyStreakWeeks: (): Promise<number> => http.get<number>("/streaks/mine"),
};
