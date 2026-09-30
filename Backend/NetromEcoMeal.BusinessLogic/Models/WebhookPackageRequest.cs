namespace NetromEcoMeal.Models;

// Body shape for POST /api/webhooks/packages — a caller keyed to one business should never be
// able to set BusinessId, IsHidden, or anything else the raw Package entity would expose.
public class WebhookPackageRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required decimal Price { get; set; }
    public required int Quantity { get; set; }
    public required decimal WeightKg { get; set; }
    public required Guid PackageTypeId { get; set; }
    public List<string>? DietaryTags { get; set; }
    public required DateTime PickupStart { get; set; }
    public required DateTime PickupEnd { get; set; }
    public string? ImageUrl { get; set; }
}
