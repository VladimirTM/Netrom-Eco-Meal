import { createContext, useContext } from "react";

export const TimeZoneContext = createContext<string>("UTC");

export function useTimeZone(): string {
  return useContext(TimeZoneContext);
}
