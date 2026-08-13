namespace Netrom_Eco_Meal.Services;

// Singleton (every other service here is Scoped — see BACKEND_ARCHITECTURE.md §5) so the event
// reaches every open BusinessDetail.razor circuit, not just the one that triggered the change —
// same OnChange + InvokeAsync(StateHasChanged) idiom CartService/ClientTimeZoneService use.
// Carries only BusinessId, not a precomputed quantity: "available stock" also depends on each
// viewer's own cart (CartService.AvailableQuantity), so subscribers re-fetch and recompute locally.
public class PackageStockBroadcaster
{
    public event Action<Guid>? BusinessStockChanged;

    public void NotifyBusinessChanged(Guid businessId) => BusinessStockChanged?.Invoke(businessId);
}
