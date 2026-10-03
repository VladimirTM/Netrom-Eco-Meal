export interface ReferralRow {
  friendName: string;
  invitedAt: string;
  rewarded: boolean;
}

export interface ReferralInfo {
  referralCode: string;
  creditBalance: number;
  referrals: ReferralRow[];
}
