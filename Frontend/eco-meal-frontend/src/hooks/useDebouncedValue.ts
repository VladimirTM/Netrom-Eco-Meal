import { useEffect, useState } from "react";

// Companion to useDebouncedCallback, for the common "debounce a controlled input's value" shape
// (Home's search box — mirrors Home.razor's own 300ms OnSearchInputAsync debounce).
export function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const handle = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(handle);
  }, [value, delayMs]);

  return debounced;
}
