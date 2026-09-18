namespace Netrom_Eco_Meal.Models;

// Response shape for POST /api/webhooks/packages — not the raw Package entity, since EF's
// change-tracker fixup can wire Package.Business.Staff[].Business into a reference cycle
// System.Text.Json rejects (see WebhookController).
public record WebhookPackageResponse(Guid Id, Guid BusinessId, string Name, decimal Price, int Quantity, decimal WeightKg, DateTime PickupStart, DateTime PickupEnd);
