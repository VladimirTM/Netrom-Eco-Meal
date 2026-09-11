// Embed this on your own website to show live impact numbers for one business, pulled from
// ImpactController's public, anonymous endpoint. Self-contained — no dependencies, inline styling.
//   <script src="https://your-ecomeal-host/js/impact-widget.js" data-business-id="YOUR-BUSINESS-ID"></script>
(function () {
    var scriptTag = document.currentScript;
    if (!scriptTag) return;

    var businessId = scriptTag.getAttribute('data-business-id');
    if (!businessId) {
        console.error('[EcoMeal] impact-widget.js: missing required data-business-id attribute.');
        return;
    }

    // Defaults to the origin this script was served from, so the snippet Dashboard.razor hands
    // out just works — data-base-url only exists as an escape hatch for an unusual hosting setup.
    var baseUrl = scriptTag.getAttribute('data-base-url');
    if (!baseUrl) {
        try {
            baseUrl = new URL(scriptTag.src).origin;
        } catch (e) {
            console.error('[EcoMeal] impact-widget.js: could not resolve base URL — pass data-base-url explicitly.');
            return;
        }
    }

    var card = document.createElement('div');
    card.setAttribute('style',
        'font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,sans-serif;' +
        'border:1px solid #cfe3cf;border-radius:12px;padding:16px 20px;max-width:320px;' +
        'background:#f4faf4;color:#20301f;line-height:1.4;');
    card.innerHTML = '<div style="font-size:13px;color:#5a7a5a;">Loading Eco Meal impact…</div>';
    scriptTag.parentNode.insertBefore(card, scriptTag.nextSibling);

    function escapeHtml(value) {
        var div = document.createElement('div');
        div.textContent = value;
        return div.innerHTML;
    }

    function fmt(n, digits) {
        return Number(n).toLocaleString(undefined, { maximumFractionDigits: digits, minimumFractionDigits: 0 });
    }

    fetch(baseUrl + '/api/businesses/' + encodeURIComponent(businessId) + '/impact')
        .then(function (res) {
            if (!res.ok) throw new Error('Eco Meal impact widget: request failed with ' + res.status);
            return res.json();
        })
        .then(function (data) {
            card.innerHTML =
                '<div style="font-weight:700;font-size:14px;margin-bottom:6px;">🌱 ' + escapeHtml(data.businessName) + ' on Eco Meal</div>' +
                '<div style="font-size:26px;font-weight:800;color:#2e7d32;">' + fmt(data.totalKgSaved, 1) + ' kg</div>' +
                '<div style="font-size:12px;color:#5a7a5a;margin-bottom:8px;">of surplus food rescued from waste</div>' +
                '<div style="font-size:12px;color:#4a5a4a;">' +
                    '≈ ' + fmt(data.co2eKgAvoided, 0) + ' kg CO2e avoided &middot; ' +
                    fmt(data.kmNotDriven, 0) + ' km not driven &middot; ' +
                    fmt(data.litersWaterSaved, 0) + ' L water saved' +
                '</div>' +
                '<a href="' + baseUrl + '/businesses/' + encodeURIComponent(businessId) + '" target="_blank" rel="noopener" ' +
                    'style="display:inline-block;margin-top:10px;font-size:12px;font-weight:600;color:#2e7d32;text-decoration:none;">' +
                    'See what’s live now →' +
                '</a>';
        })
        .catch(function () {
            card.innerHTML = '<div style="font-size:12px;color:#a03434;">Couldn’t load Eco Meal impact stats right now.</div>';
        });
})();
