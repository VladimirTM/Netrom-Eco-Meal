namespace NetromEcoMeal.Models;

// Shape returned by the public, anonymous /api/businesses/{id}/impact endpoint, called from
// wwwroot/js/impact-widget.js on a business's own website. TotalKgSaved includes donated packages
// alongside Completed-order pickups (ImpactService.GetBusinessWidgetStatsAsync); KgSavedThisMonth
// is order-pickups only.
public record BusinessImpactWidgetDto(
    Guid BusinessId,
    string BusinessName,
    decimal TotalKgSaved,
    decimal KgSavedThisMonth,
    int CompletedOrders,
    decimal Co2eKgAvoided,
    decimal KmNotDriven,
    decimal LitersWaterSaved);
