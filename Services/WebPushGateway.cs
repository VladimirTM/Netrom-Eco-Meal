using System.Net;
using System.Text.Json;
using Netrom_Eco_Meal.Services.Interfaces;
using WebPush;

namespace Netrom_Eco_Meal.Services;

// Reads WebPush:PublicKey/PrivateKey/Subject straight from IConfiguration, same style as
// StripeGateway's Stripe:*. Unlike Stripe this needs no external account — the keypair is a
// self-generated VAPID key (VapidHelper.GenerateVapidKeys), so it ships pre-configured for local
// dev; a blank config just degrades to "no push sent," same as SmtpEmailSender's missing Host.
public class WebPushGateway : IWebPushGateway
{
    private readonly WebPushClient _client = new();
    private readonly VapidDetails? _vapidDetails;
    private readonly ILogger<WebPushGateway> _logger;

    public WebPushGateway(IConfiguration configuration, ILogger<WebPushGateway> logger)
    {
        _logger = logger;
        PublicKey = configuration["WebPush:PublicKey"];
        var privateKey = configuration["WebPush:PrivateKey"];
        var subject = configuration["WebPush:Subject"];

        if (string.IsNullOrWhiteSpace(PublicKey) || string.IsNullOrWhiteSpace(privateKey) || string.IsNullOrWhiteSpace(subject))
            return;

        _vapidDetails = new VapidDetails(subject, PublicKey, privateKey);
    }

    public bool IsConfigured => _vapidDetails is not null;
    public string? PublicKey { get; }

    public async Task<bool> SendAsync(Entities.PushSubscription subscription, string title, string body, string? url)
    {
        if (_vapidDetails is null)
            return true;

        var payload = JsonSerializer.Serialize(new { title, body, url });
        var pushSubscription = new PushSubscription(subscription.Endpoint, subscription.P256Dh, subscription.Auth);

        try
        {
            await _client.SendNotificationAsync(pushSubscription, payload, _vapidDetails);
            return true;
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            // The push service (browser vendor) confirms this subscription no longer exists —
            // an uninstalled/reset browser, e.g. — so the caller should stop trying it.
            return false;
        }
        catch (Exception ex)
        {
            // Best-effort like every other notification channel — a push failure never blocks
            // the underlying transition. Not necessarily a dead subscription, so it's kept.
            _logger.LogWarning(ex, "Web push send failed for endpoint {Endpoint}", subscription.Endpoint);
            return true;
        }
    }
}
