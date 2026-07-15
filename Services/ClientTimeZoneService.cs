using Microsoft.JSInterop;

namespace Netrom_Eco_Meal.Services;

// Resolves the browser's IANA timezone via JS interop so UTC-stored pickup windows can be shown
// in the viewer's local time. Consuming pages subscribe to OnChange (same pattern as CartService).
public class ClientTimeZoneService(IJSRuntime jsRuntime)
{
    private bool _initialized;

    public TimeZoneInfo TimeZone { get; private set; } = TimeZoneInfo.Utc;

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;

        try
        {
            var id = await jsRuntime.InvokeAsync<string?>("EcoMeal.timeZone");
            TimeZone = id is null ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch
        {
            TimeZone = TimeZoneInfo.Utc;
        }

        OnChange?.Invoke();
    }

    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);

    public DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZone);
}
