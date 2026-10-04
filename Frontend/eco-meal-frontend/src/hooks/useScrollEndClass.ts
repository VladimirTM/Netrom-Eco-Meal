import { useEffect, useRef } from "react";

// Toggles `.at-scroll-end` on a horizontally-scrollable element once it's scrolled all the way to
// its right edge — clears the CSS trailing-edge fade (index.css's "Fades the trailing edge as a
// swipe hint" rule on .public-header-inner .d-flex.gap-2) once there's nothing left to scroll to.
// Replaces the delegated scroll listener Blazor's site.js used for this.
export function useScrollEndClass<T extends HTMLElement>() {
  const ref = useRef<T>(null);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;

    function update() {
      if (!el) return;
      const atEnd = el.scrollWidth - el.scrollLeft - el.clientWidth < 1;
      el.classList.toggle("at-scroll-end", atEnd);
    }

    update();
    el.addEventListener("scroll", update, { passive: true });
    const observer = new ResizeObserver(update);
    observer.observe(el);
    return () => {
      el.removeEventListener("scroll", update);
      observer.disconnect();
    };
  }, []);

  return ref;
}
