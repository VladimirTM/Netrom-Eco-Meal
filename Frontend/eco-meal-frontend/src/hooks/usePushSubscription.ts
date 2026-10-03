import { useCallback } from "react";

export interface PushSubscriptionInfo {
  endpoint: string;
  p256dh: string;
  auth: string;
}

export function isPushSupported(): boolean {
  return "serviceWorker" in navigator && "PushManager" in window && "Notification" in window;
}

// Fire-and-forget, called once on every app load regardless of whether the viewer ever opens the
// notification panel — registration itself needs no permission. Mirrors site.js's own unconditional
// EcoMeal.push.registerAsync() call.
export function registerServiceWorker(): void {
  if (!isPushSupported()) return;
  navigator.serviceWorker.register("/service-worker.js").catch(() => {});
}

// VAPID applicationServerKey must be a Uint8Array, not the base64url string the server hands back.
function vapidKeyToUint8Array(base64String: string): Uint8Array {
  const padding = "=".repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, "+").replace(/_/g, "/");
  const rawData = atob(base64);
  const outputArray = new Uint8Array(rawData.length);
  for (let i = 0; i < rawData.length; ++i) outputArray[i] = rawData.charCodeAt(i);
  return outputArray;
}

// Ports site.js's EcoMeal.push namespace. Every method resolves, never rejects — the caller never
// needs a try/catch, same convention as useGeolocation.
export function usePushSubscription() {
  const getSubscriptionEndpoint = useCallback(async (): Promise<string | null> => {
    if (!isPushSupported()) return null;
    const registration = await navigator.serviceWorker.ready.catch(() => null);
    if (!registration) return null;
    const subscription = await registration.pushManager.getSubscription();
    return subscription ? subscription.endpoint : null;
  }, []);

  // Prompts for Notification permission if needed, then subscribes. Resolves to
  // {endpoint, p256dh, auth} for the caller to hand to PushSubscriptionsController, or null on
  // denial/failure/unsupported.
  const subscribe = useCallback(async (publicKeyBase64: string): Promise<PushSubscriptionInfo | null> => {
    if (!isPushSupported() || !publicKeyBase64) return null;

    const permission = await Notification.requestPermission();
    if (permission !== "granted") return null;

    const registration = await navigator.serviceWorker.ready.catch(() => null);
    if (!registration) return null;

    try {
      const existing = await registration.pushManager.getSubscription();
      const subscription =
        existing ??
        (await registration.pushManager.subscribe({
          userVisibleOnly: true,
          applicationServerKey: vapidKeyToUint8Array(publicKeyBase64) as BufferSource,
        }));
      const json = subscription.toJSON();
      return { endpoint: json.endpoint!, p256dh: json.keys!.p256dh, auth: json.keys!.auth };
    } catch {
      return null;
    }
  }, []);

  // Unsubscribes the browser's current subscription and returns its endpoint (so the caller can
  // tell the backend to delete the matching row), or null if there wasn't one.
  const unsubscribe = useCallback(async (): Promise<string | null> => {
    if (!isPushSupported()) return null;
    const registration = await navigator.serviceWorker.ready.catch(() => null);
    if (!registration) return null;

    const subscription = await registration.pushManager.getSubscription();
    if (!subscription) return null;

    const endpoint = subscription.endpoint;
    await subscription.unsubscribe();
    return endpoint;
  }, []);

  return { isSupported: isPushSupported, getSubscriptionEndpoint, subscribe, unsubscribe };
}
