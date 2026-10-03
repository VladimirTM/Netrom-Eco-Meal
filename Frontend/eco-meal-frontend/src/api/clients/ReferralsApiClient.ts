import { http } from "../base/http";
import type { ReferralInfo } from "../models/Referral";

export const referralsApi = {
  getMine: (): Promise<ReferralInfo> => http.get<ReferralInfo>("/referrals/mine"),
};
