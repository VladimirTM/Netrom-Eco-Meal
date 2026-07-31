using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services;

public class CartItem
{
    public required Package Package { get; init; }
    public int Quantity { get; set; }
}

// Restores via its own short-lived DbContext (IDbContextFactory), not the shared per-circuit one —
// a first-render restore used to race the routed page's own query on that context and crash the
// circuit. Same fix as NotificationRepository's polling bell.
public class CartService(IJSRuntime jsRuntime, IDbContextFactory<EcoMealDbContext> contextFactory, CurrentUserAccessor currentUserAccessor)
{
    private const string BaseStorageKey = "ecomeal.cart";

    private readonly List<CartItem> _items = [];
    private bool _restored;
    private string? _userId;

    // Namespaced per signed-in user (set once in RestoreAsync) so a basket left over from a session
    // that ended without an explicit Sign Out can't be picked up by a different account. Falls back
    // to the bare key before a user is resolved, or on the admin/manager layout, which never carts.
    public string StorageKey => _userId is null ? BaseStorageKey : $"{BaseStorageKey}.{_userId}";

    public Guid? BusinessId { get; private set; }
    public string? BusinessName { get; private set; }
    public IReadOnlyList<CartItem> Items => _items;
    public int TotalCount => _items.Sum(i => i.Quantity);
    public decimal TotalPrice => _items.Sum(i => i.Quantity * i.Package.Price);

    public event Action? OnChange;

    public bool WouldReplaceCart(Package package) => BusinessId is not null && BusinessId != package.BusinessId;

    public int InCartQuantity(Guid packageId) => _items.FirstOrDefault(i => i.Package.Id == packageId)?.Quantity ?? 0;

    // reservedElsewhere: other Pending reservations against this package, not just the local cart.
    public int AvailableQuantity(Package package, int reservedElsewhere = 0) =>
        Math.Max(0, package.Quantity - reservedElsewhere - InCartQuantity(package.Id));

    public async Task RestoreAsync()
    {
        if (_restored)
            return;
        _restored = true;

        (_, _userId) = await currentUserAccessor.GetCurrentUserAsync();

        List<StoredCartItem>? stored;
        try
        {
            var json = await jsRuntime.InvokeAsync<string?>("EcoMeal.cart.load", StorageKey);
            if (string.IsNullOrWhiteSpace(json))
                return;

            stored = JsonSerializer.Deserialize<List<StoredCartItem>>(json);
        }
        catch
        {
            return;
        }

        if (stored is null || stored.Count == 0)
            return;

        var ids = stored.Select(s => s.PackageId).ToList();
        await using var context = await contextFactory.CreateDbContextAsync();
        var packages = await context.Packages
            .Include(p => p.PackageType)
            .Include(p => p.Business)
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();
        foreach (var entry in stored)
        {
            var package = packages.FirstOrDefault(p => p.Id == entry.PackageId);
            if (package is not null)
                AddInternal(package, entry.Quantity);
        }

        OnChange?.Invoke();
    }

    public async Task AddAsync(Package package, int quantity = 1)
    {
        if (!AddInternal(package, quantity))
            return;

        await PersistAsync();
        OnChange?.Invoke();
    }

    public async Task SetQuantityAsync(Guid packageId, int quantity)
    {
        var item = _items.FirstOrDefault(i => i.Package.Id == packageId);
        if (item is null)
            return;

        if (quantity <= 0)
        {
            await RemoveAsync(packageId);
            return;
        }

        item.Quantity = Math.Min(quantity, item.Package.Quantity);
        await PersistAsync();
        OnChange?.Invoke();
    }

    public async Task RemoveAsync(Guid packageId)
    {
        _items.RemoveAll(i => i.Package.Id == packageId);
        if (_items.Count == 0)
        {
            BusinessId = null;
            BusinessName = null;
        }

        await PersistAsync();
        OnChange?.Invoke();
    }

    public async Task ClearAsync()
    {
        _items.Clear();
        BusinessId = null;
        BusinessName = null;
        await PersistAsync();
        OnChange?.Invoke();
    }

    private bool AddInternal(Package package, int quantity)
    {
        if (package.Quantity <= 0)
            return false;

        if (WouldReplaceCart(package))
        {
            _items.Clear();
        }

        BusinessId = package.BusinessId;
        BusinessName = package.Business.Name;

        var existing = _items.FirstOrDefault(i => i.Package.Id == package.Id);
        if (existing is not null)
            existing.Quantity = Math.Min(existing.Quantity + quantity, package.Quantity);
        else
            _items.Add(new CartItem { Package = package, Quantity = Math.Clamp(quantity, 1, package.Quantity) });

        return true;
    }

    private async Task PersistAsync()
    {
        var stored = _items.Select(i => new StoredCartItem(i.Package.Id, i.Quantity)).ToList();
        var json = JsonSerializer.Serialize(stored);
        try
        {
            await jsRuntime.InvokeVoidAsync("EcoMeal.cart.save", StorageKey, json);
        }
        catch
        {
            // Best-effort: the in-memory cart stays authoritative even if this write fails.
        }
    }

    private record StoredCartItem(Guid PackageId, int Quantity);
}
