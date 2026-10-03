// Registered by EcoMeal.push.registerAsync (site.js) on every page load. The only two jobs a
// Web Push service worker needs: turn a push message into a system notification, and route a
// click on that notification back into the app.

self.addEventListener("push", function (event) {
    var data = {};
    try {
        data = event.data ? event.data.json() : {};
    } catch {
        data = { body: event.data ? event.data.text() : "" };
    }

    var title = data.title || "Eco Meal";
    var options = {
        body: data.body || "",
        icon: "/logo.png",
        badge: "/favicon.png",
        data: { url: data.url || "/" }
    };

    event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener("notificationclick", function (event) {
    event.notification.close();
    var targetUrl = new URL((event.notification.data && event.notification.data.url) || "/", self.location.origin).href;

    event.waitUntil(
        self.clients.matchAll({ type: "window", includeUncontrolled: true }).then(function (clientList) {
            for (var i = 0; i < clientList.length; i++) {
                if ("focus" in clientList[i]) {
                    clientList[i].navigate(targetUrl);
                    return clientList[i].focus();
                }
            }
            if (self.clients.openWindow) {
                return self.clients.openWindow(targetUrl);
            }
        })
    );
});
