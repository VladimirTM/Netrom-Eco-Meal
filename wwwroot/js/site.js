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
    }
};

EcoMeal.push.registerAsync();
