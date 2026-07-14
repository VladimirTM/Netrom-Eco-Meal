window.EcoMeal = {
    // Escapes ancestor overflow clipping (e.g. table-responsive); flips above the
    // trigger when there's not enough room below.
    positionDropdown: function (anchorEl, dropdownEl) {
        if (!anchorEl || !dropdownEl) return;

        const anchorRect = anchorEl.getBoundingClientRect();
        const dropdownHeight = dropdownEl.offsetHeight;
        const dropdownWidth = dropdownEl.offsetWidth;
        const margin = 6;

        const spaceBelow = window.innerHeight - anchorRect.bottom;
        const openUpward = spaceBelow < dropdownHeight + margin && anchorRect.top > dropdownHeight + margin;

        const top = openUpward
            ? anchorRect.top - dropdownHeight - margin
            : anchorRect.bottom + margin;

        let left = anchorRect.right - dropdownWidth;
        left = Math.max(8, Math.min(left, window.innerWidth - dropdownWidth - 8));

        dropdownEl.style.top = Math.max(8, top) + "px";
        dropdownEl.style.left = left + "px";
        dropdownEl.style.visibility = "visible";
    },

    // Survives reloads — CartService is per-circuit and would otherwise reset.
    cart: {
        save: function (key, json) {
            try {
                localStorage.setItem(key, json);
            } catch { /* storage unavailable (private browsing, quota, etc.) */ }
        },
        load: function (key) {
            try {
                return localStorage.getItem(key);
            } catch {
                return null;
            }
        },
        clear: function (key) {
            try {
                localStorage.removeItem(key);
            } catch { /* storage unavailable (private browsing, quota, etc.) */ }
        }
    }
};
