using Netrom_Eco_Meal.Services.Interfaces;
using Stripe;
using Stripe.Checkout;

namespace Netrom_Eco_Meal.Services.Payments;

// Reads Stripe:SecretKey/Stripe:Currency straight from IConfiguration, same style as
// SmtpEmailSender's Email:Smtp:* — one small settings surface, not worth a bound options class.
// Program.cs sets Stripe.StripeConfiguration.ApiKey globally at startup when the key is present;
// EnsureConfigured below just turns "key missing" into a friendly error instead of a raw Stripe
// SDK exception, mirroring how SmtpEmailSender degrades when Email:Smtp:Host isn't set.
public class StripeGateway(IConfiguration configuration) : IStripeGateway
{
    private string Currency => (configuration["Stripe:Currency"] ?? "ron").ToLowerInvariant();

    private static void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(StripeConfiguration.ApiKey))
            throw new InvalidOperationException("Payments aren't configured yet — set Stripe:SecretKey to enable checkout.");
    }

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        Guid pendingCheckoutId, string businessName, List<CheckoutLineItem> lines, string successUrl, string cancelUrl)
    {
        EnsureConfigured();

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = pendingCheckoutId.ToString(),
            LineItems = lines.Select(line => new SessionLineItemOptions
            {
                Quantity = line.Quantity,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = Currency,
                    UnitAmount = (long)Math.Round(line.UnitPrice * 100m, MidpointRounding.AwayFromZero),
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = $"{businessName} — {line.PackageName}",
                    },
                },
            }).ToList(),
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);
        return new CheckoutSessionResult(session.Id, session.Url);
    }

    public async Task<StripeSessionStatus> GetSessionStatusAsync(string sessionId)
    {
        EnsureConfigured();

        var service = new SessionService();
        var session = await service.GetAsync(sessionId);
        return new StripeSessionStatus(
            session.PaymentStatus == "paid",
            session.PaymentIntentId,
            (session.AmountTotal ?? 0) / 100m,
            session.Currency ?? Currency);
    }

    public async Task RefundAsync(string paymentIntentId)
    {
        EnsureConfigured();

        var service = new RefundService();
        await service.CreateAsync(new RefundCreateOptions { PaymentIntent = paymentIntentId });
    }
}
