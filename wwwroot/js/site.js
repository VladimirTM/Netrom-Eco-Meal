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

        // Belt-and-suspenders on the flip logic above — clamps both edges so an anchor sitting
        // a few px past the true viewport can't still push the panel off-screen.
        const clampedTop = Math.max(8, Math.min(top, window.innerHeight - dropdownHeight - 8));

        dropdownEl.style.top = clampedTop + "px";
        dropdownEl.style.left = left + "px";
        dropdownEl.style.visibility = "visible";
    },

    // Backs SafeFocusOnNavigate.razor — only moves focus if nothing else has it yet, since the
    // framework's own FocusOnNavigate can steal focus from a field the user's already typing in.
    a11y: {
        focusIfIdle: function (selector) {
            var active = document.activeElement;
            if (active && active !== document.body && active !== document.documentElement) {
                return;
            }
            var el = document.querySelector(selector);
            if (el) el.focus();
        }
    },

    // Disables a plain form's submit button on submit so a second click can't double-fire the
    // request — for the data-enhance="false" forms (Login/Register/change-password), which have
    // no Blazor @code handler to guard this with a busy flag.
    forms: {
        preventDoubleSubmit: function (formEl) {
            var btn = formEl.querySelector("button[type=submit]");
            if (btn) btn.disabled = true;
        }
    },

    // Lets the server convert stored UTC pickup windows to the viewer's own local time.
    timeZone: function () {
        try {
            return Intl.DateTimeFormat().resolvedOptions().timeZone;
        } catch {
            return null;
        }
    },

    // Browser geolocation for "near me" sort and the location picker on BusinessForm. Resolves to
    // null (never rejects) on denial/timeout/unsupported, so callers don't need a try/catch.
    geo: {
        getPosition: function () {
            return new Promise(function (resolve) {
                if (!navigator.geolocation) {
                    resolve(null);
                    return;
                }
                navigator.geolocation.getCurrentPosition(
                    function (pos) { resolve({ lat: pos.coords.latitude, lng: pos.coords.longitude }); },
                    function () { resolve(null); },
                    { timeout: 8000 }
                );
            });
        }
    },

    // Renders/updates a Leaflet map of business pins — see Home.razor's map view toggle. Leaflet
    // is loaded via CDN in App.razor, no build step or API key required (OpenStreetMap tiles).
    map: {
        _instances: {},
        render: function (elementId, markers) {
            var el = document.getElementById(elementId);
            if (!el || typeof L === "undefined") return;

            if (this._instances[elementId]) {
                this._instances[elementId].remove();
                delete this._instances[elementId];
            }

            var map = L.map(elementId);
            L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
                attribution: "&copy; OpenStreetMap contributors",
                maxZoom: 19
            }).addTo(map);

            var bounds = [];
            markers.forEach(function (m) {
                var marker = L.marker([m.lat, m.lng]).addTo(map);
                marker.bindPopup("<strong>" + escapeHtml(m.name) + "</strong><br/><a href=\"/businesses/" + m.id + "\">View kitchen</a>");
                bounds.push([m.lat, m.lng]);
            });

            if (bounds.length > 0) {
                map.fitBounds(bounds, { padding: [30, 30] });
            } else {
                // Timișoara — falls back to the seed data's city.
                map.setView([45.7489, 21.2087], 12);
            }

            this._instances[elementId] = map;

            function escapeHtml(s) {
                return s.replace(/[&<>"']/g, function (c) {
                    return { "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[c];
                });
            }
        },
        destroy: function (elementId) {
            if (this._instances[elementId]) {
                this._instances[elementId].remove();
                delete this._instances[elementId];
            }
        }
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
    },

    // Manual dark/light toggle (UI revamp Phase 2/3). Sets data-bs-theme alongside data-theme so
    // Bootstrap's own dark mode covers raw .card/.table/.form-control that --em-* tokens don't.
    theme: {
        KEY: "em-theme",
        get: function () {
            return document.documentElement.getAttribute("data-theme") || "light";
        },
        set: function (theme) {
            document.documentElement.setAttribute("data-theme", theme);
            document.documentElement.setAttribute("data-bs-theme", theme);
            try {
                localStorage.setItem(this.KEY, theme);
            } catch { /* storage unavailable (private browsing, quota, etc.) */ }
        },
        toggle: function () {
            var next = this.get() === "dark" ? "light" : "dark";
            this.set(next);
            return next;
        }
    },

    // Persists which business a multi-business staff member is currently managing — same
    // survives-reload need as cart, above.
    managedBusiness: {
        save: function (key, businessId) {
            try {
                localStorage.setItem(key, businessId);
            } catch { /* storage unavailable (private browsing, quota, etc.) */ }
        },
        load: function (key) {
            try {
                return localStorage.getItem(key);
            } catch {
                return null;
            }
        }
    },

    // Web Push: service-worker registration + subscribe/unsubscribe. NotificationPanel.razor is
    // the one caller (its "enable browser alerts" toggle) — actually sending a push once
    // subscribed is entirely server-side, see PushSubscriptionService/WebPushGateway.
    push: {
        isSupported: function () {
            return "serviceWorker" in navigator && "PushManager" in window && "Notification" in window;
        },

        // Fire-and-forget, called once below on every page load regardless of whether the viewer
        // ever opens the notification panel — registration itself needs no permission.
        registerAsync: function () {
            if (!this.isSupported()) return Promise.resolve(null);
            return navigator.serviceWorker.register("/service-worker.js").catch(function () { return null; });
        },

        // VAPID applicationServerKey must be a Uint8Array, not the base64url string the server hands back.
        _vapidKeyToUint8Array: function (base64String) {
            var padding = "=".repeat((4 - (base64String.length % 4)) % 4);
            var base64 = (base64String + padding).replace(/-/g, "+").replace(/_/g, "/");
            var rawData = atob(base64);
            var outputArray = new Uint8Array(rawData.length);
            for (var i = 0; i < rawData.length; ++i) {
                outputArray[i] = rawData.charCodeAt(i);
            }
            return outputArray;
        },

        // Resolves to the current subscription's endpoint, or null if never subscribed — used to
        // initialize the toggle's on/off state on page load without prompting for permission.
        getSubscriptionEndpoint: async function () {
            if (!this.isSupported()) return null;
            var registration = await navigator.serviceWorker.ready.catch(function () { return null; });
            if (!registration) return null;
            var subscription = await registration.pushManager.getSubscription();
            return subscription ? subscription.endpoint : null;
        },

        // Prompts for Notification permission if needed, then subscribes. Resolves to
        // {endpoint, p256dh, auth} for the caller to hand to PushSubscriptionController, or null
        // on denial/failure/unsupported — never rejects, same convention as geo.getPosition.
        subscribe: async function (publicKeyBase64) {
            if (!this.isSupported() || !publicKeyBase64) return null;

            var permission = await Notification.requestPermission();
            if (permission !== "granted") return null;

            var registration = await navigator.serviceWorker.ready.catch(function () { return null; });
            if (!registration) return null;

            try {
                var existing = await registration.pushManager.getSubscription();
                var subscription = existing || await registration.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: this._vapidKeyToUint8Array(publicKeyBase64)
                });
                var json = subscription.toJSON();
                return { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth };
            } catch {
                return null;
            }
        },

        // Unsubscribes the browser's current subscription and returns its endpoint (so the
        // caller can tell the backend to delete the matching row), or null if there wasn't one.
        unsubscribe: async function () {
            if (!this.isSupported()) return null;
            var registration = await navigator.serviceWorker.ready.catch(function () { return null; });
            if (!registration) return null;

            var subscription = await registration.pushManager.getSubscription();
            if (!subscription) return null;

            var endpoint = subscription.endpoint;
            await subscription.unsubscribe();
            return endpoint;
        }
    },

    // Backs the business-card placeholder icon (Home.razor's .biz-card-media). CSS background-image
    // has no failure event, so a set-but-unreachable ImageUrl can only be caught by trying to load
    // it here; an unset one is marked failed immediately, no network round-trip needed.
    media: {
        checkPlaceholders: function () {
            document.querySelectorAll(".biz-card-media[data-image-url]").forEach(function (el) {
                if (el.dataset.placeholderChecked) return;
                el.dataset.placeholderChecked = "1";

                var url = el.dataset.imageUrl;
                if (!url) {
                    el.classList.add("biz-image-failed");
                    return;
                }

                var probe = new Image();
                probe.onerror = function () { el.classList.add("biz-image-failed"); };
                probe.src = url;
            });
        }
    }
};

EcoMeal.push.registerAsync();

// Toggles the header icon row's scroll-end fade (app.css .at-scroll-end). Delegated at the
// document level, capture phase, since scroll doesn't bubble — keeps working across re-renders.
document.addEventListener("scroll", function (e) {
    var row = e.target;
    if (row && row.classList && row.classList.contains("gap-2") && row.closest && row.closest(".public-header-inner")) {
        var atEnd = row.scrollLeft + row.clientWidth >= row.scrollWidth - 1;
        row.classList.toggle("at-scroll-end", atEnd);
    }
}, { capture: true, passive: true });

// Business cards get re-rendered wholesale on pagination/filtering, so a one-time DOMContentLoaded
// scan would miss every page after the first — the observer catches new cards as they appear.
EcoMeal.media.checkPlaceholders();
new MutationObserver(function () {
    EcoMeal.media.checkPlaceholders();
}).observe(document.body, { childList: true, subtree: true });
