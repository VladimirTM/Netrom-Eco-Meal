import { useEffect } from "react";
import { useLocation } from "react-router-dom";

// Replaces SafeFocusOnNavigate.razor + site.js's EcoMeal.a11y helpers. React Router's client-side
// navigation never triggers the browser's own "new page starts at the top" behavior, so both the
// scroll reset and the focus move have to happen here instead.
function ScrollAndFocusManager() {
  const location = useLocation();

  useEffect(() => {
    window.scrollTo(0, 0);

    // Only move focus if nothing else already has it — a stolen focus mid-navigation is exactly
    // the 2026-09-07 regression (a field the user was already typing into lost focus on nav).
    const active = document.activeElement;
    const isIdle = !active || active === document.body || active === document.documentElement;
    if (isIdle) {
      const heading = document.querySelector<HTMLElement>("h1");
      if (heading) {
        // A plain <h1> isn't natively focusable — give it a tabindex first, same as Blazor's
        // own built-in FocusOnNavigate does under the hood.
        if (!heading.hasAttribute("tabindex")) heading.setAttribute("tabindex", "-1");
        heading.focus();
      }
    }
  }, [location.pathname, location.search]);

  return null;
}

export default ScrollAndFocusManager;
