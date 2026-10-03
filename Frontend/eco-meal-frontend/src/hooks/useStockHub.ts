import * as signalR from "@microsoft/signalr";
import { useEffect, useLayoutEffect, useRef } from "react";
import { TOKEN_KEY } from "../api/base/http";

// VITE_API_URL always ends with /api (see .env.example); the hub is mounted on the host root.
function hubUrl(): string {
  const apiBase = import.meta.env.VITE_API_URL as string;
  return `${apiBase.replace(/\/api\/?$/, "")}/hubs/stock`;
}

// Replaces PackageStockBroadcaster's in-process C# event — StockHub broadcasts to group
// "business:{id}" whenever PackageService/OrderService changes that business's stock.
// Connecting/joining is best-effort: a page that can't reach the hub still works off its own
// fetch-on-load data, just without the live push, so failures here are swallowed rather than
// surfaced as page errors.
export function useStockHub(businessId: string | undefined, onChanged: () => void) {
  const onChangedRef = useRef(onChanged);
  useLayoutEffect(() => {
    onChangedRef.current = onChanged;
  });

  useEffect(() => {
    if (!businessId) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl(), {
        accessTokenFactory: () => localStorage.getItem(TOKEN_KEY) ?? "",
        // Auth here is the bearer token above, never a cookie — @microsoft/signalr defaults
        // withCredentials to true, which the Api's any-origin dev CORS policy (no
        // AllowCredentials) then rejects outright: the preflight OPTIONS still returns 204, but
        // the browser discards the real response for not echoing back
        // Access-Control-Allow-Credentials, surfacing as a bare "Failed to fetch" with no server
        // log at all. Turning it off avoids needing a credentialed CORS policy for a connection
        // that was never credentialed to begin with.
        withCredentials: false,
      })
      .withAutomaticReconnect()
      .build();

    connection.on("BusinessStockChanged", (changedBusinessId: string) => {
      if (changedBusinessId === businessId) onChangedRef.current();
    });

    const join = () => connection.invoke("JoinBusinessGroup", businessId).catch(() => {});
    connection.onreconnected(join);

    let disposed = false;
    connection
      .start()
      .then(() => {
        if (!disposed) return join();
      })
      .catch(() => {});

    return () => {
      disposed = true;
      connection.stop().catch(() => {});
    };
  }, [businessId]);
}
