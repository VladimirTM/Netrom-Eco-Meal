using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services;

// Per-circuit "which of my businesses am I managing right now" — a business manager can now be
// staff at more than one business (see BusinessStaff), so unlike the old single ManagerId lookup,
// "my business" needs an explicit, switchable selection. Same scoped-service + localStorage
// persistence pattern as CartService.
//
// Queries via its own short-lived DbContext (IDbContextFactory), not the shared per-circuit one —
// this loads from NavMenu's OnInitializedAsync, which runs concurrently with the routed page's own
// OnInitializedAsync on that shared context and would otherwise crash the circuit ("a second
// operation was started on this context instance"). Same fix as CartService's restore.
public class ManagedBusinessContext(IJSRuntime jsRuntime, IDbContextFactory<EcoMealDbContext> contextFactory, CurrentUserAccessor currentUser)
{
    private const string BaseStorageKey = "ecomeal.managedBusiness";

    // NavMenu (layout) and the routed page both call EnsureLoadedAsync from their own
    // OnInitializedAsync, which can interleave before either finishes. Caching the in-flight
    // Task itself (not a bool flipped before the awaited load completes) means a concurrent
    // second caller awaits the same load instead of reading MyBusinesses/SelectedBusinessId
    // before the first caller ever populated them.
    private Task? _loadTask;
    private string? _userId;

    public List<Business> MyBusinesses { get; private set; } = [];
    public Guid? SelectedBusinessId { get; private set; }

    public event Action? OnChange;

    private string StorageKey => _userId is null ? BaseStorageKey : $"{BaseStorageKey}.{_userId}";

    public Task EnsureLoadedAsync() => _loadTask ??= LoadAsync();

    private async Task LoadAsync()
    {
        (_, _userId) = await currentUser.GetCurrentUserAsync();
        if (_userId is null)
            return;

        await using var context = await contextFactory.CreateDbContextAsync();
        MyBusinesses = await context.Businesses
            .Include(b => b.BusinessType)
            .Where(b => b.Staff.Any(s => s.UserId == _userId))
            .OrderBy(b => b.Name)
            .ToListAsync();

        Guid? stored = null;
        try
        {
            var raw = await jsRuntime.InvokeAsync<string?>("EcoMeal.managedBusiness.load", StorageKey);
            if (Guid.TryParse(raw, out var parsed))
                stored = parsed;
        }
        catch
        {
            // Storage unavailable — fall back to the default selection below.
        }

        SelectedBusinessId = MyBusinesses.Any(b => b.Id == stored) ? stored : MyBusinesses.FirstOrDefault()?.Id;
    }

    public async Task SelectAsync(Guid businessId)
    {
        if (!MyBusinesses.Any(b => b.Id == businessId) || businessId == SelectedBusinessId)
            return;

        SelectedBusinessId = businessId;
        try
        {
            await jsRuntime.InvokeVoidAsync("EcoMeal.managedBusiness.save", StorageKey, businessId.ToString());
        }
        catch
        {
            // Best-effort: the in-memory selection stays authoritative even if this write fails.
        }

        OnChange?.Invoke();
    }
}
