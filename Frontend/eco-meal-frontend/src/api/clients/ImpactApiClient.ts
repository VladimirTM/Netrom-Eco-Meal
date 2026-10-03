import { http } from "../base/http";
import type { LeaderboardEntry } from "../models/Impact";

export const impactApi = {
  getMonthlyLeaderboard: (take = 20): Promise<LeaderboardEntry[]> => http.get<LeaderboardEntry[]>(`/impact/leaderboard?take=${take}`),

  getMyOptInStatus: (): Promise<boolean> => http.get<boolean>("/impact/opt-in/mine"),

  setMyOptInStatus: (showOnLeaderboard: boolean): Promise<void> => http.put<void>("/impact/opt-in/mine", { showOnLeaderboard }),
};
