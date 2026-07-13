using System.Text.Json;
using Microsoft.JSInterop;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class CartItem
{
    public required Package Package { get; init; }
    public int Quantity { get; set; }
}

public class CartService(IJSRuntime jsRuntime, IPackageService packageService)
{
    public const string StorageKey = "ecomeal.cart";

    private readonly List<CartItem> _items = [];
    private bool _restored;

    public Guid? BusinessId { get; private set; }
    public string? BusinessName { get; private set; }
    public IReadOnlyList<CartItem> Items => _items;
    public int TotalCount => _items.Sum(i => i.Quantity);
    public decimal TotalPrice => _items.Sum(i => i.Quantity * i.Package.Price);

    public event Action? OnChange;

    public bool WouldReplaceCart(Package package) => BusinessId is not null && BusinessId != package.BusinessId;

    public int InCartQuantity(Guid packageId) => _items.FirstOrDefault(i => i.Package.Id == packageId)?.Quantity ?? 0;

    public int AvailableQuantity(Package package) => Math.Max(0, package.Quantity - InCartQuantity(package.Id));

    public async Task RestoreAsync()
    {
        if (_restored)
            return;
        _restored = true;

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

        var packages = await packageService.GetByIdsAsync(stored.Select(s => s.PackageId));
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
            // Best-effort persistence — the in-memory cart for this circuit stays authoritative
            // even if the browser-side write failed (e.g. mid-reconnect).
        }
    }

    private record StoredCartItem(Guid PackageId, int Quantity);
}
