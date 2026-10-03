import { useCallback, useEffect, useLayoutEffect, useRef } from "react";

// Replaces Debouncer (Backend/NetromEcoMeal.Web/Models/Debouncer.cs): collapses rapid triggers
// (keystrokes, quick clicks) into a single call. The original's DbContext-race concern doesn't
// apply here — there's no shared per-circuit DbContext in a browser tab — so a plain
// clear-and-reschedule timer is enough.
export function useDebouncedCallback<Args extends unknown[]>(callback: (...args: Args) => void, delayMs: number) {
  const callbackRef = useRef(callback);
  useLayoutEffect(() => {
    callbackRef.current = callback;
  });
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(
    () => () => {
      if (timeoutRef.current) clearTimeout(timeoutRef.current);
    },
    [],
  );

  return useCallback(
    (...args: Args) => {
      if (timeoutRef.current) clearTimeout(timeoutRef.current);
      if (delayMs <= 0) {
        callbackRef.current(...args);
        return;
      }
      timeoutRef.current = setTimeout(() => callbackRef.current(...args), delayMs);
    },
    [delayMs],
  );
}
