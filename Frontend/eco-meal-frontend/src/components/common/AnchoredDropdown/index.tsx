import { useLayoutEffect, useRef, useState, type ReactNode } from "react";

interface AnchoredDropdownProps {
  trigger: ReactNode;
  children: ReactNode;
  triggerClass?: string;
  panelClass?: string;
  disabled?: boolean;
  title?: string;
  isOpen: boolean;
  onToggle: () => void;
  onClose: () => void;
}

// Ports Shared/AnchoredDropdown.razor + site.js's EcoMeal.positionDropdown — `.role-dropdown` is
// `position: fixed` (it needs to escape ancestor overflow/stacking contexts, e.g. the sidebar's own
// scroll container), so its top/left are computed from the trigger's own bounding rect rather than
// placed with plain CSS, with the same open-upward-if-no-room and viewport-clamping rules.
function AnchoredDropdown({ trigger, children, triggerClass = "role-badge", panelClass = "role-dropdown", disabled, title, isOpen, onToggle, onClose }: AnchoredDropdownProps) {
  const anchorRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const [style, setStyle] = useState<{ top: number; left: number; visibility: "visible" | "hidden" }>({ top: 0, left: 0, visibility: "hidden" });

  useLayoutEffect(() => {
    if (!isOpen) return;
    const anchorEl = anchorRef.current;
    const panelEl = panelRef.current;
    if (!anchorEl || !panelEl) return;

    const anchorRect = anchorEl.getBoundingClientRect();
    const dropdownHeight = panelEl.offsetHeight;
    const dropdownWidth = panelEl.offsetWidth;
    const margin = 6;

    const spaceBelow = window.innerHeight - anchorRect.bottom;
    const openUpward = spaceBelow < dropdownHeight + margin && anchorRect.top > dropdownHeight + margin;

    const top = openUpward ? anchorRect.top - dropdownHeight - margin : anchorRect.bottom + margin;

    let left = anchorRect.right - dropdownWidth;
    left = Math.max(8, Math.min(left, window.innerWidth - dropdownWidth - 8));

    const clampedTop = Math.max(8, Math.min(top, window.innerHeight - dropdownHeight - 8));

    setStyle({ top: clampedTop, left, visibility: "visible" });
  }, [isOpen]);

  return (
    <>
      <button type="button" ref={anchorRef} className={triggerClass} disabled={disabled} title={title} onClick={onToggle}>
        {trigger}
      </button>

      {isOpen && (
        <>
          <div className="role-dropdown-backdrop" onClick={onClose} />
          <div className={panelClass} ref={panelRef} style={{ top: style.top, left: style.left, visibility: style.visibility }}>
            {children}
          </div>
        </>
      )}
    </>
  );
}

export default AnchoredDropdown;
