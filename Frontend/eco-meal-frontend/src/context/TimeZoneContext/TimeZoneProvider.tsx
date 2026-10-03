import { useState, type ReactNode } from "react";
import { TimeZoneContext } from "./timezone-context";

// Replaces ClientTimeZoneService's JS-interop round trip — the browser's own Intl API already
// has this, no server call needed.
function detectTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone;
  } catch {
    return "UTC";
  }
}

function TimeZoneProvider({ children }: { children: ReactNode }) {
  const [timeZone] = useState(detectTimeZone);
  return <TimeZoneContext.Provider value={timeZone}>{children}</TimeZoneContext.Provider>;
}

export default TimeZoneProvider;
