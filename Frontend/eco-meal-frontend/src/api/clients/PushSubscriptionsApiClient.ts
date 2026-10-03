import { http } from "../base/http";

export const pushSubscriptionsApi = {
  // Anonymous — null when WebPush isn't configured server-side (the toggle then stays hidden).
  getPublicKey: (): Promise<string | null> => http.get<string | null>("/push-subscriptions/public-key"),

  subscribe: (endpoint: string, p256dh: string, auth: string): Promise<void> =>
    http.post<void>("/push-subscriptions", { endpoint, p256dh, auth }),

  unsubscribe: (endpoint: string): Promise<void> => http.post<void>("/push-subscriptions/unsubscribe", { endpoint }),
};
